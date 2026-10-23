using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace FryCircuit.Core.Control;

/// <summary>
/// Immutable representation of a polynomial in Laplace variable s:
/// P(s) = c_n * s^n + c_{n-1} * s^{n-1} + ... + c_1 * s + c_0
/// Coefficients are stored in descending powers: [c_n, c_{n-1}, ..., c_0].
/// </summary>
public sealed class Polynomial : IEquatable<Polynomial>
{
    private readonly double[] _coeffs;

    /// <summary>Coefficients in descending powers of s: [c_n, c_{n-1}, ..., c_0]</summary>
    public IReadOnlyList<double> Coefficients => _coeffs;

    /// <summary>Degree of the polynomial. Degree of 0 is 0 (constant).</summary>
    public int Degree => Math.Max(0, _coeffs.Length - 1);

    /// <summary>True if the polynomial is identically zero.</summary>
    public bool IsZero => _coeffs.Length == 1 && Math.Abs(_coeffs[0]) < 1e-15;

    public static readonly Polynomial Zero = new(0.0);
    public static readonly Polynomial One = new(1.0);
    public static readonly Polynomial S = new(1.0, 0.0);

    public Polynomial(params double[] coefficients)
    {
        if (coefficients == null || coefficients.Length == 0)
        {
            _coeffs = new[] { 0.0 };
            return;
        }

        // Strip leading zeros
        int firstNonZero = 0;
        while (firstNonZero < coefficients.Length - 1 && Math.Abs(coefficients[firstNonZero]) < 1e-15)
        {
            firstNonZero++;
        }

        int count = coefficients.Length - firstNonZero;
        _coeffs = new double[count];
        Array.Copy(coefficients, firstNonZero, _coeffs, 0, count);
    }

    public static Polynomial FromRoots(params Complex[] roots)
    {
        if (roots == null || roots.Length == 0)
            return One;

        // Multiply (s - r_1)(s - r_2)...
        // Note: Complex conjugate roots result in real polynomial
        Complex[] c = { Complex.One };
        foreach (var r in roots)
        {
            var next = new Complex[c.Length + 1];
            for (int i = 0; i < c.Length; i++)
            {
                next[i] += c[i];        // s * c[i]
                next[i + 1] -= c[i] * r; // -r * c[i]
            }
            c = next;
        }

        double[] realCoeffs = new double[c.Length];
        for (int i = 0; i < c.Length; i++)
        {
            realCoeffs[i] = c[i].Real;
        }
        return new Polynomial(realCoeffs);
    }

    public static Polynomial Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Zero;

        // Supports comma/space separated coefficients: "1, 6, 100" or "s^2 + 6s + 100"
        text = text.Trim();

        // Check if comma or space separated numbers
        var parts = text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var parsedList = new List<double>();
        bool allNumbers = true;
        foreach (var p in parts)
        {
            if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                parsedList.Add(val);
            }
            else
            {
                allNumbers = false;
                break;
            }
        }

        if (allNumbers && parsedList.Count > 0)
        {
            return new Polynomial(parsedList.ToArray());
        }

        // Otherwise parse algebraic string: e.g. "s^2 + 6s + 100"
        return ParseAlgebraic(text);
    }

    private static Polynomial ParseAlgebraic(string s)
    {
        s = s.Replace(" ", "").Replace("-", "+-");
        var terms = s.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var map = new Dictionary<int, double>();

        foreach (var rawTerm in terms)
        {
            string term = rawTerm.Trim();
            if (string.IsNullOrEmpty(term)) continue;

            int power;
            double coeff;

            if (!term.Contains('s') && !term.Contains('S'))
            {
                power = 0;
                coeff = double.Parse(term, CultureInfo.InvariantCulture);
            }
            else
            {
                int sIdx = term.IndexOfAny(new[] { 's', 'S' });
                string coeffPart = term.Substring(0, sIdx);
                if (coeffPart == "" || coeffPart == "+") coeff = 1.0;
                else if (coeffPart == "-") coeff = -1.0;
                else coeff = double.Parse(coeffPart, CultureInfo.InvariantCulture);

                string rest = term.Substring(sIdx + 1);
                if (rest.StartsWith("^"))
                {
                    power = int.Parse(rest.Substring(1), CultureInfo.InvariantCulture);
                }
                else
                {
                    power = 1;
                }
            }

            map[power] = map.GetValueOrDefault(power, 0.0) + coeff;
        }

        if (map.Count == 0) return Zero;
        int maxPower = map.Keys.Max();
        double[] coeffs = new double[maxPower + 1];
        for (int p = maxPower; p >= 0; p--)
        {
            coeffs[maxPower - p] = map.GetValueOrDefault(p, 0.0);
        }
        return new Polynomial(coeffs);
    }

    /// <summary>Evaluates P(s) at real value s using Horner's rule.</summary>
    public double Evaluate(double s)
    {
        double result = 0.0;
        for (int i = 0; i < _coeffs.Length; i++)
        {
            result = result * s + _coeffs[i];
        }
        return result;
    }

    /// <summary>Evaluates P(s) at complex value s = sigma + j*omega using Horner's rule.</summary>
    public Complex Evaluate(Complex s)
    {
        Complex result = Complex.Zero;
        for (int i = 0; i < _coeffs.Length; i++)
        {
            result = result * s + _coeffs[i];
        }
        return result;
    }

    /// <summary>Derivative dP/ds.</summary>
    public Polynomial Derivative()
    {
        if (Degree == 0) return Zero;
        double[] dCoeffs = new double[Degree];
        for (int i = 0; i < Degree; i++)
        {
            int power = Degree - i;
            dCoeffs[i] = _coeffs[i] * power;
        }
        return new Polynomial(dCoeffs);
    }

    /// <summary>Indefinite integral with constant term 0.</summary>
    public Polynomial Integral()
    {
        if (IsZero) return Zero;
        double[] intCoeffs = new double[_coeffs.Length + 1];
        for (int i = 0; i < _coeffs.Length; i++)
        {
            int power = _coeffs.Length - i;
            intCoeffs[i] = _coeffs[i] / power;
        }
        intCoeffs[^1] = 0.0;
        return new Polynomial(intCoeffs);
    }

    /// <summary>Finds all roots of P(s) = 0.</summary>
    public IReadOnlyList<Complex> Roots() => FindRoots();

    /// <summary>Finds all roots of P(s) = 0.</summary>
    public IReadOnlyList<Complex> FindRoots()
    {
        if (Degree <= 0) return Array.Empty<Complex>();

        if (Degree == 1)
        {
            // a*s + b = 0 => s = -b/a
            return new[] { new Complex(-_coeffs[1] / _coeffs[0], 0.0) };
        }

        if (Degree == 2)
        {
            double a = _coeffs[0];
            double b = _coeffs[1];
            double c = _coeffs[2];
            double disc = b * b - 4 * a * c;

            if (disc >= 0)
            {
                double sqrt = Math.Sqrt(disc);
                double q = -0.5 * (b + (b >= 0 ? sqrt : -sqrt));
                double r1 = q / a;
                double r2 = Math.Abs(q) > 1e-15 ? c / q : 0.0;
                return new[] { new Complex(r1, 0.0), new Complex(r2, 0.0) };
            }
            else
            {
                double real = -b / (2 * a);
                double imag = Math.Sqrt(-disc) / (2 * a);
                return new[] { new Complex(real, imag), new Complex(real, -imag) };
            }
        }

        // Higher degree: solve via Frobenius Companion Matrix eigenvalues (Francis QR algorithm)
        return CompanionMatrixEigenvalues();
    }

    private List<Complex> CompanionMatrixEigenvalues()
    {
        int n = Degree;
        double lead = _coeffs[0];
        if (Math.Abs(lead) < 1e-15) lead = 1.0;

        // Build companion matrix:
        // [-a_{n-1}/a_n, -a_{n-2}/a_n, ..., -a_0/a_n]
        // [     1      ,       0      , ...,     0   ]
        // [     0      ,       1      , ...,     0   ]
        double[,] A = new double[n, n];
        for (int j = 0; j < n; j++)
        {
            A[0, j] = -_coeffs[j + 1] / lead;
        }
        for (int i = 1; i < n; i++)
        {
            A[i, i - 1] = 1.0;
        }

        return QrEigenvalues(A, n);
    }

    private static List<Complex> QrEigenvalues(double[,] H, int n)
    {
        var roots = new List<Complex>();
        int m = n;
        int maxIter = 150 * n;
        int iter = 0;

        while (m > 2 && iter < maxIter)
        {
            // Check for negligible subdiagonal element
            int l = m - 1;
            while (l > 0)
            {
                double s = Math.Abs(H[l - 1, l - 1]) + Math.Abs(H[l, l]);
                if (s == 0.0) s = 1.0;
                if (Math.Abs(H[l, l - 1]) <= 1e-14 * s)
                {
                    H[l, l - 1] = 0.0;
                    break;
                }
                l--;
            }

            if (l == m - 1)
            {
                // 1x1 block converged
                roots.Add(new Complex(H[m - 1, m - 1], 0.0));
                m--;
                continue;
            }

            if (l == m - 2)
            {
                // 2x2 block converged
                roots.AddRange(Solve2x2Eigenvalues(H, m - 2, m - 1));
                m -= 2;
                continue;
            }

            // Francis double-shift QR step
            iter++;
            double s1 = H[m - 1, m - 1] + H[m - 2, m - 2];
            double t = H[m - 1, m - 1] * H[m - 2, m - 2] - H[m - 1, m - 2] * H[m - 2, m - 1];

            double x = H[l, l] * H[l, l] - s1 * H[l, l] + t + H[l, l + 1] * H[l + 1, l];
            double y = H[l + 1, l] * (H[l, l] + H[l + 1, l + 1] - s1);
            double z = H[l + 1, l] * H[l + 2, l + 1];

            for (int k = l; k < m - 1; k++)
            {
                bool notLast = (k < m - 2);
                if (k > l)
                {
                    x = H[k, k - 1];
                    y = H[k + 1, k - 1];
                    z = notLast ? H[k + 2, k - 1] : 0.0;
                }

                // Householder reflector v
                double norm = Math.Sqrt(x * x + y * y + z * z);
                if (norm > 1e-15)
                {
                    if (x < 0) norm = -norm;
                    x += norm;
                    double v1 = y / x;
                    double v2 = z / x;
                    double beta = 2.0 / (1.0 + v1 * v1 + v2 * v2);

                    // Apply H = P * H * P
                    int maxC = Math.Min(m, notLast ? k + 4 : m);
                    for (int j = (k > l ? k - 1 : l); j < m; j++)
                    {
                        double p = beta * (H[k, j] + v1 * H[k + 1, j] + (notLast ? v2 * H[k + 2, j] : 0.0));
                        H[k, j] -= p;
                        H[k + 1, j] -= p * v1;
                        if (notLast) H[k + 2, j] -= p * v2;
                    }

                    int minR = Math.Max(0, l);
                    for (int i = minR; i < maxC; i++)
                    {
                        double p = beta * (H[i, k] + v1 * H[i, k + 1] + (notLast ? v2 * H[i, k + 2] : 0.0));
                        H[i, k] -= p;
                        H[i, k + 1] -= p * v1;
                        if (notLast) H[i, k + 2] -= p * v2;
                    }
                }
            }
        }

        if (m == 2)
        {
            roots.AddRange(Solve2x2Eigenvalues(H, 0, 1));
        }
        else if (m == 1)
        {
            roots.Add(new Complex(H[0, 0], 0.0));
        }

        return roots.OrderBy(r => r.Real).ThenBy(r => r.Imaginary).ToList();
    }

    private static IEnumerable<Complex> Solve2x2Eigenvalues(double[,] H, int i1, int i2)
    {
        double a = H[i1, i1];
        double b = H[i1, i2];
        double c = H[i2, i1];
        double d = H[i2, i2];

        double tr = a + d;
        double det = a * d - b * c;
        double disc = tr * tr - 4 * det;

        if (disc >= 0)
        {
            double sqrt = Math.Sqrt(disc);
            return new[] { new Complex((tr + sqrt) * 0.5, 0.0), new Complex((tr - sqrt) * 0.5, 0.0) };
        }
        else
        {
            double real = tr * 0.5;
            double imag = Math.Sqrt(-disc) * 0.5;
            return new[] { new Complex(real, imag), new Complex(real, -imag) };
        }
    }

    #region Operators

    public static Polynomial operator +(Polynomial a, Polynomial b)
    {
        int maxLen = Math.Max(a._coeffs.Length, b._coeffs.Length);
        double[] result = new double[maxLen];

        for (int i = 0; i < maxLen; i++)
        {
            int aIdx = a._coeffs.Length - 1 - i;
            int bIdx = b._coeffs.Length - 1 - i;
            double aVal = aIdx >= 0 ? a._coeffs[aIdx] : 0.0;
            double bVal = bIdx >= 0 ? b._coeffs[bIdx] : 0.0;
            result[maxLen - 1 - i] = aVal + bVal;
        }

        return new Polynomial(result);
    }

    public static Polynomial operator -(Polynomial a, Polynomial b)
    {
        int maxLen = Math.Max(a._coeffs.Length, b._coeffs.Length);
        double[] result = new double[maxLen];

        for (int i = 0; i < maxLen; i++)
        {
            int aIdx = a._coeffs.Length - 1 - i;
            int bIdx = b._coeffs.Length - 1 - i;
            double aVal = aIdx >= 0 ? a._coeffs[aIdx] : 0.0;
            double bVal = bIdx >= 0 ? b._coeffs[bIdx] : 0.0;
            result[maxLen - 1 - i] = aVal - bVal;
        }

        return new Polynomial(result);
    }

    public static Polynomial operator *(Polynomial a, Polynomial b)
    {
        if (a.IsZero || b.IsZero) return Zero;

        int len = a._coeffs.Length + b._coeffs.Length - 1;
        double[] result = new double[len];

        for (int i = 0; i < a._coeffs.Length; i++)
        {
            for (int j = 0; j < b._coeffs.Length; j++)
            {
                result[i + j] += a._coeffs[i] * b._coeffs[j];
            }
        }

        return new Polynomial(result);
    }

    public static Polynomial operator *(Polynomial a, double scalar)
    {
        if (Math.Abs(scalar) < 1e-15 || a.IsZero) return Zero;
        double[] result = new double[a._coeffs.Length];
        for (int i = 0; i < a._coeffs.Length; i++)
        {
            result[i] = a._coeffs[i] * scalar;
        }
        return new Polynomial(result);
    }

    public static Polynomial operator *(double scalar, Polynomial a) => a * scalar;

    public static Polynomial operator /(Polynomial a, double scalar)
    {
        if (Math.Abs(scalar) < 1e-15) throw new DivideByZeroException();
        return a * (1.0 / scalar);
    }

    public static Polynomial operator -(Polynomial a) => a * -1.0;

    #endregion

    public override string ToString()
    {
        if (IsZero) return "0";

        var sb = new StringBuilder();
        int deg = Degree;

        for (int i = 0; i < _coeffs.Length; i++)
        {
            double c = _coeffs[i];
            if (Math.Abs(c) < 1e-12) continue;

            int power = deg - i;
            bool isFirst = sb.Length == 0;

            if (c > 0)
            {
                if (!isFirst) sb.Append(" + ");
            }
            else
            {
                if (!isFirst) sb.Append(" - ");
                else sb.Append("-");
                c = -c;
            }

            bool showCoeff = Math.Abs(c - 1.0) > 1e-9 || power == 0;
            if (showCoeff)
            {
                sb.Append(c.ToString("G4", CultureInfo.InvariantCulture));
            }

            if (power > 0)
            {
                if (showCoeff) sb.Append("");
                sb.Append("s");
                if (power > 1)
                {
                    sb.Append('^').Append(power);
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : "0";
    }

    /// <summary>
    /// Formats polynomial using standard academic/textbook notation with unicode superscripts (e.g. s² + 6s + 100).
    /// </summary>
    public string ToAcademicString(string variable = "s") => AcademicMathFormatter.ToAcademicPolynomial(this, variable);

    public bool Equals(Polynomial? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_coeffs.Length != other._coeffs.Length) return false;

        for (int i = 0; i < _coeffs.Length; i++)
        {
            if (Math.Abs(_coeffs[i] - other._coeffs[i]) > 1e-9) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is Polynomial p && Equals(p);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (var c in _coeffs)
        {
            hash = hash * 31 + Math.Round(c, 8).GetHashCode();
        }
        return hash;
    }
}
