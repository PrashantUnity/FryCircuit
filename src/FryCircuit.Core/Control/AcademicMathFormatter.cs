using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace FryCircuit.Core.Control;

/// <summary>
/// Academic Root specification for textbook-grade mathematical presentation of poles and zeros.
/// </summary>
public sealed record AcademicRoot(
    string Label,            // e.g. "s₁,₂", "s₃", "z₁"
    string ValueString,      // e.g. "-0.03 ± j1.15", "-0.66"
    double Real,
    double ImaginaryMagnitude,
    double? DampingRatioZeta,// e.g. 0.026 for oscillatory poles
    double? NaturalFreqWn,   // e.g. 1.15 rad/s
    bool IsComplexPair,
    bool IsRhp,              // Real > 1e-5 (unstable)
    bool IsImaginaryAxis     // |Real| <= 1e-5 && Imag > 1e-5 (marginally stable / oscillatory)
)
{
    public string DampingDetails => DampingRatioZeta.HasValue && NaturalFreqWn.HasValue
        ? $"ζ = {DampingRatioZeta.Value:F3}, ωn = {NaturalFreqWn.Value:F2} rad/s"
        : string.Empty;
}

/// <summary>
/// Academic and textbook-grade mathematical formatter for polynomials, transfer functions, and eigenvalues.
/// Converts raw computer representations into elegant mathematical notation standard in control theory (Åström-Murray, Ogata).
/// </summary>
public static class AcademicMathFormatter
{
    /// <summary>
    /// Formats a list of roots (poles or zeros) into standard academic notation:
    /// - Purely real roots are formatted as real numbers (never with +0.00j).
    /// - Complex conjugate pairs are grouped into s = σ ± jω with damping ratio ζ and natural frequency ωn.
    /// - Subscript indexing (s₁,₂, s₃, etc.).
    /// </summary>
    public static List<AcademicRoot> GroupAndFormatRoots(IReadOnlyList<Complex> rawRoots, string prefix = "s")
    {
        var result = new List<AcademicRoot>();
        if (rawRoots == null || rawRoots.Count == 0) return result;

        bool[] handled = new bool[rawRoots.Count];

        // Sort roots by dominant impact: descending real part (closest to RHP first)
        var indexed = rawRoots.Select((r, i) => (Root: r, OrigIndex: i))
                              .OrderByDescending(x => x.Root.Real)
                              .ThenByDescending(x => Math.Abs(x.Root.Imaginary))
                              .ToList();

        int rootIndex = 1;

        for (int i = 0; i < indexed.Count; i++)
        {
            int origI = indexed[i].OrigIndex;
            if (handled[origI]) continue;

            Complex c1 = indexed[i].Root;

            // 1. Purely Real Root
            if (Math.Abs(c1.Imaginary) < 1e-5)
            {
                handled[origI] = true;
                string label = $"{prefix}{FormatSubscript(rootIndex++)}";
                string valStr = FormatRealNumber(c1.Real);
                bool isRhp = c1.Real > 1e-5;
                result.Add(new AcademicRoot(label, valStr, c1.Real, 0.0, null, null, false, isRhp, false));
                continue;
            }

            // 2. Look for conjugate partner: c2.Real ≈ c1.Real and c2.Imag ≈ -c1.Imag
            int conjugateIdx = -1;
            for (int j = i + 1; j < indexed.Count; j++)
            {
                int origJ = indexed[j].OrigIndex;
                if (handled[origJ]) continue;

                Complex c2 = indexed[j].Root;
                if (Math.Abs(c1.Real - c2.Real) < 1e-4 && Math.Abs(c1.Imaginary + c2.Imaginary) < 1e-4)
                {
                    conjugateIdx = j;
                    break;
                }
            }

            if (conjugateIdx >= 0)
            {
                int origJ = indexed[conjugateIdx].OrigIndex;
                handled[origI] = true;
                handled[origJ] = true;

                int idx1 = rootIndex++;
                int idx2 = rootIndex++;
                string label = $"{prefix}{FormatSubscript(idx1)},{FormatSubscript(idx2)}";

                double sigma = c1.Real;
                double omegaD = Math.Abs(c1.Imaginary);
                double omegaN = Math.Sqrt(sigma * sigma + omegaD * omegaD);
                double zeta = omegaN > 1e-6 ? -sigma / omegaN : 0.0;

                string valStr;
                if (Math.Abs(sigma) < 1e-5)
                {
                    valStr = $"± j{omegaD:F2}";
                }
                else
                {
                    valStr = $"{sigma:F2} ± j{omegaD:F2}";
                }

                bool isRhp = sigma > 1e-5;
                bool isImag = Math.Abs(sigma) <= 1e-5 && omegaD > 1e-5;
                result.Add(new AcademicRoot(label, valStr, sigma, omegaD, zeta, omegaN, true, isRhp, isImag));
            }
            else
            {
                // Unpaired complex root (fallback for general complex inputs)
                handled[origI] = true;
                string label = $"{prefix}{FormatSubscript(rootIndex++)}";
                string sign = c1.Imaginary >= 0 ? "+" : "-";
                string valStr = $"{c1.Real:F2} {sign} j{Math.Abs(c1.Imaginary):F2}";
                bool isRhp = c1.Real > 1e-5;
                bool isImag = Math.Abs(c1.Real) <= 1e-5 && Math.Abs(c1.Imaginary) > 1e-5;
                result.Add(new AcademicRoot(label, valStr, c1.Real, Math.Abs(c1.Imaginary), null, null, false, isRhp, isImag));
            }
        }

        return result;
    }

    /// <summary>
    /// Formats a polynomial with unicode superscripts: s² + 6s + 100
    /// </summary>
    public static string ToAcademicPolynomial(Polynomial poly, string variable = "s")
    {
        if (poly == null || poly.IsZero) return "0";

        var sb = new StringBuilder();
        int deg = poly.Degree;
        var coeffs = poly.Coefficients;

        for (int i = 0; i < coeffs.Count; i++)
        {
            double c = coeffs[i];
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
                sb.Append(variable);
                if (power > 1)
                {
                    sb.Append(FormatSuperscript(power));
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : "0";
    }

    /// <summary>
    /// Formats a discrete Z-domain polynomial: b₀ + b₁z⁻¹ + b₂z⁻²
    /// </summary>
    public static string ToDiscretePolynomial(IReadOnlyList<double> coeffs, string variable = "z")
    {
        if (coeffs == null || coeffs.Count == 0) return "0";

        var sb = new StringBuilder();
        for (int i = 0; i < coeffs.Count; i++)
        {
            double c = coeffs[i];
            if (Math.Abs(c) < 1e-12) continue;

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

            bool showCoeff = Math.Abs(c - 1.0) > 1e-9 || i == 0;
            if (showCoeff)
            {
                sb.Append(c.ToString("G4", CultureInfo.InvariantCulture));
            }

            if (i > 0)
            {
                sb.Append(variable).Append("⁻").Append(FormatSuperscript(i));
            }
        }

        return sb.Length > 0 ? sb.ToString() : "0";
    }

    /// <summary>
    /// Converts a number to unicode subscript (e.g. 1 -> ₁, 12 -> ₁₂).
    /// </summary>
    public static string FormatSubscript(int num)
    {
        string s = num.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        foreach (char ch in s)
        {
            sb.Append(ch switch
            {
                '0' => '₀',
                '1' => '₁',
                '2' => '₂',
                '3' => '₃',
                '4' => '₄',
                '5' => '₅',
                '6' => '₆',
                '7' => '₇',
                '8' => '₈',
                '9' => '₉',
                _ => ch
            });
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts a number to unicode superscript (e.g. 2 -> ², 3 -> ³, -1 -> ⁻¹).
    /// </summary>
    public static string FormatSuperscript(int num)
    {
        string s = num.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        foreach (char ch in s)
        {
            sb.Append(ch switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                '-' => '⁻',
                _ => ch
            });
        }
        return sb.ToString();
    }

    private static string FormatRealNumber(double d)
    {
        if (Math.Abs(d) < 1e-5) return "0";
        // If near integer, show clean integer or 2 decimal places
        if (Math.Abs(d - Math.Round(d)) < 1e-4)
            return Math.Round(d).ToString("F0", CultureInfo.InvariantCulture);
        return d.ToString("F2", CultureInfo.InvariantCulture);
    }
}
