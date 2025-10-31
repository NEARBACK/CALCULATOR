using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CalcApp.Views;

public partial class MainWindow : Window
{
    private string _currentInput = "0";
    private decimal _accumulator = 0m;
    private char? _pendingOp = null; // '+', '-', '*', '/'
    private bool _justEvaluated = false;
    private bool IsError() => _currentInput == "Error";


    public MainWindow()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Content is null)
            return;

        var key = btn.Content.ToString();

        switch (key)
        {
            case "0": case "1": case "2": case "3": case "4":
            case "5": case "6": case "7": case "8": case "9":
                AppendDigit(key);
                break;

            case ".":
                AppendDecimalPoint();
                break;

            case "+": case "−": case "×": case "÷":
                ApplyOperator(ToOperatorChar(key));
                break;

            case "=":
                Evaluate();
                break;

            case "C":
                ClearAll();
                break;

            case "⌫":
                Backspace();
                break;
            case "√":
                ApplySqrt();
                break;

            case "^":
                ApplyOperator('^');
                break;
            case "y√x":
                ApplyOperator('r'); // наш бинарный оператор «корень степени y из x»
                break;
        }

        UpdateDisplay();
    }

    private void AppendDigit(string d)
    {
        if (_justEvaluated || IsError())
        {
            _currentInput = "0";
            _justEvaluated = false;
        }

        if (_currentInput == "0") _currentInput = d;
        else _currentInput += d;
    }

    private void AppendDecimalPoint()
    {
        if (_justEvaluated || IsError())
        {
            _currentInput = "0";
            _justEvaluated = false;
        }

        if (!_currentInput.Contains('.'))
            _currentInput += ".";
    }

    private void ApplyOperator(char op)
    {
        if (decimal.TryParse(_currentInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            if (_pendingOp is null)
            {
                _accumulator = value;
            }
            else
            {
                _accumulator = Compute(_accumulator, value, _pendingOp.Value);
            }

            _pendingOp = op;
            _currentInput = "0";
            _justEvaluated = false;
        }
    }

    private void Evaluate()
    {
        if (_pendingOp is not null &&
            decimal.TryParse(_currentInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            // Доп. защита для y√x: отрицательный x при чётной/нецелой степени
            if (_pendingOp == 'r')
            {
                var n = _accumulator; // степень
                double dn = (double)n;
                double dx = (double)value;
                bool nIsInt = Math.Abs(dn - Math.Round(dn)) < 1e-12;
                bool nIsOdd = nIsInt && (Math.Abs(Math.Round(dn)) % 2 == 1);

                if (dx < 0 && !(nIsInt && nIsOdd))
                {
                    _currentInput = "Error";
                    _pendingOp = null;
                    _justEvaluated = false;
                    UpdateDisplay();
                    return;
                }
            }

            _accumulator = Compute(_accumulator, value, _pendingOp.Value);
            _currentInput = ToDisplayString(_accumulator);
            _pendingOp = null;
            _justEvaluated = true;
        }
    }


    private void ClearAll()
    {
        _currentInput = "0";
        _accumulator = 0m;
        _pendingOp = null;
        _justEvaluated = false;
    }

    private void Backspace()
    {
        if (_justEvaluated || IsError()) return;
        if (_currentInput.Length <= 1 || (_currentInput.Length == 2 && _currentInput.StartsWith("-")))
            _currentInput = "0";
        else
            _currentInput = _currentInput[..^1];
    }
    private void ApplySqrt()
    {
        if (IsError()) return;

        if (decimal.TryParse(_currentInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0)
            {
                // Нельзя корень из отрицательного — покажем ошибку
                _currentInput = "Error";
                _pendingOp = null;
                _justEvaluated = false;
                return;
            }

            // Math.Sqrt работает с double — конвертируем аккуратно.
            var result = (decimal)Math.Sqrt((double)value);
            _currentInput = ToDisplayString(result);
            _justEvaluated = true; // как будто нажали '=' для унарной операции
        }
    }

    private static decimal PowDecimal(decimal a, decimal b)
    {
        // Внимание: через double — возможны небольшие погрешности представления
        // Для учебного калькулятора это нормально.
        try
        {
            double da = (double)a;
            double db = (double)b;
            double dr = Math.Pow(da, db);

            if (double.IsNaN(dr) || double.IsInfinity(dr))
                return 0m;

            return (decimal)dr;
        }
        catch
        {
            return 0m;
        }
    }


    private static decimal Compute(decimal a, decimal b, char op)
    {
        return op switch
        {
            '+' => a + b,
            '-' => a - b,
            '*' => a * b,
            '/' => b == 0m ? 0m : a / b,
            '^' => PowDecimal(a, b),
            'r' => NthRootDecimal(b, a), // a = степень (y), b = подкоренное (x)
            _ => b
        };
    }
    
    private static decimal NthRootDecimal(decimal x, decimal n)
    {
        // Обработка особых случаев
        if (n == 0m) return 0m; // корень нулевой степени — не определён
        double dx = (double)x;
        double dn = (double)n;

        // Проверим «n — целое?» и «n — нечётное?»
        bool nIsInt = Math.Abs(dn - Math.Round(dn)) < 1e-12;
        bool nIsOdd = nIsInt && (Math.Abs(Math.Round(dn)) % 2 == 1);

        // Отрицательное подкоренное — только для нечётных целых степеней
        if (dx < 0 && !(nIsInt && nIsOdd))
            return 0m; // недопустимо -> вернём 0 (для простоты)

        try
        {
            double res;
            if (dx < 0) // нечётная степень: корень отрицательного — отрицательный
            {
                res = -Math.Pow(Math.Abs(dx), 1.0 / Math.Abs(dn));
            }
            else
            {
                res = Math.Pow(dx, 1.0 / dn);
            }

            if (double.IsNaN(res) || double.IsInfinity(res))
                return 0m;

            return (decimal)res;
        }
        catch
        {
            return 0m;
        }
    }


    private static char ToOperatorChar(string s) => s switch
    {
        "+" => '+',
        "−" => '-',
        "×" => '*',
        "÷" => '/',
        "^" => '^',
        "y√x" => 'r',   // <---
        _   => '+'
    };


    private void UpdateDisplay()
    {
        if (Display is not null)
            Display.Text = _currentInput;
    }

    private static string ToDisplayString(decimal v)
    {
        // Уберём лишние нули, но оставим точность
        return v.ToString("0.################", CultureInfo.InvariantCulture);
    }
}
