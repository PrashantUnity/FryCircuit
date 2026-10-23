using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace FryCircuit.Core.Control;

/// <summary>
/// Continuous-time Single-Input Single-Output (SISO) Linear Time-Invariant (LTI) transfer function:
/// G(s) = N(s) / D(s) = (b_m * s^m + ... + b_0) / (a_n * s^n + ... + a_0)
/// </summary>
public sealed class TransferFunction : IEquatable<TransferFunction>
{
    public Polynomial Numerator { get; }
    public Polynomial Denominator { get; }

    public IReadOnlyList<Complex> Poles => Denominator.FindRoots();
    public IReadOnlyList<Complex> Zeros => Numerator.FindRoots();

    /// <summary>Degree of denominator minus degree of numerator.</summary>
    public int RelativeDegree => Denominator.Degree - Numerator.Degree;

    /// <summary>True if deg(N) &lt;= deg(D).</summary>
    public bool IsProper => Numerator.Degree <= Denominator.Degree;

    /// <summary>True if deg(N) &lt; deg(D).</summary>
    public bool IsStrictlyProper => Numerator.Degree < Denominator.Degree;

    /// <summary>True if all poles have strictly negative real parts (Hurwitz denominator).</summary>
    public bool IsStable => Poles.All(p => p.Real < -1e-12);

    /// <summary>DC Gain: G(0) = N(0) / D(0).</summary>
    public double DcGain
    {
        get
        {
            double d0 = Denominator.Evaluate(0.0);
            if (Math.Abs(d0) < 1e-15)
            {
                // Pole at origin
                return double.PositiveInfinity;
            }
            return Numerator.Evaluate(0.0) / d0;
        }
    }

    /// <summary>High-frequency gain: lim_{s -> infinity} G(s).</summary>
    public double HighFrequencyGain
    {
        get
        {
            if (Numerator.Degree < Denominator.Degree) return 0.0;
            if (Numerator.Degree > Denominator.Degree) return double.PositiveInfinity;
            return Numerator.Coefficients[0] / Denominator.Coefficients[0];
        }
    }

    public static readonly TransferFunction Zero = new(Polynomial.Zero, Polynomial.One);
    public static readonly TransferFunction One = new(Polynomial.One, Polynomial.One);

    public TransferFunction(Polynomial numerator, Polynomial denominator)
    {
        if (denominator.IsZero)
            throw new ArgumentException("Denominator cannot be zero.", nameof(denominator));

        // Normalize leading coefficient of denominator to 1 if possible
        double lead = denominator.Coefficients[0];
        if (Math.Abs(lead - 1.0) > 1e-12 && Math.Abs(lead) > 1e-15)
        {
            Numerator = numerator / lead;
            Denominator = denominator / lead;
        }
        else
        {
            Numerator = numerator;
            Denominator = denominator;
        }
    }

    public TransferFunction(double[] numerator, double[] denominator)
        : this(new Polynomial(numerator), new Polynomial(denominator))
    {
    }

    public static TransferFunction Parse(string numString, string denString)
    {
        var num = Polynomial.Parse(numString);
        var den = Polynomial.Parse(denString);
        if (den.IsZero) den = Polynomial.One;
        return new TransferFunction(num, den);
    }

    /// <summary>Evaluates frequency response G(j*omega) at frequency omega in rad/s.</summary>
    public Complex Evaluate(double omega) => Evaluate(new Complex(0.0, omega));

    /// <summary>Evaluates transfer function G(s) at complex frequency s.</summary>
    public Complex Evaluate(Complex s)
    {
        var num = Numerator.Evaluate(s);
        var den = Denominator.Evaluate(s);
        if (Complex.Abs(den) < 1e-18) return new Complex(1e12, 0.0);
        return num / den;
    }

    /// <summary>Instance helper for closed-loop feedback: this / (1 - sign * this * feedback).</summary>
    public TransferFunction Feedback(TransferFunction? feedback = null, int sign = -1)
    {
        return Feedback(this, feedback, sign);
    }

    /// <summary>Series connection: G1 * G2.</summary>
    public static TransferFunction Series(TransferFunction g1, TransferFunction g2)
    {
        return new TransferFunction(g1.Numerator * g2.Numerator, g1.Denominator * g2.Denominator);
    }

    /// <summary>Parallel connection: G1 + G2.</summary>
    public static TransferFunction Parallel(TransferFunction g1, TransferFunction g2)
    {
        var num = g1.Numerator * g2.Denominator + g2.Numerator * g1.Denominator;
        var den = g1.Denominator * g2.Denominator;
        return new TransferFunction(num, den);
    }

    /// <summary>
    /// Closed-loop feedback connection:
    /// For negative feedback (sign = -1): T = G1 / (1 + G1 * G2).
    /// For positive feedback (sign = +1): T = G1 / (1 - G1 * G2).
    /// </summary>
    public static TransferFunction Feedback(TransferFunction forward, TransferFunction? feedback = null, int sign = -1)
    {
        feedback ??= One;

        // T = forward / (1 - sign * forward * feedback)
        // With forward = N1/D1 and feedback = N2/D2:
        // T = (N1 * D2) / (D1 * D2 - sign * N1 * N2)
        var num = forward.Numerator * feedback.Denominator;
        var den = (sign < 0)
            ? (forward.Denominator * feedback.Denominator + forward.Numerator * feedback.Numerator)
            : (forward.Denominator * feedback.Denominator - forward.Numerator * feedback.Numerator);

        return new TransferFunction(num, den);
    }

    /// <summary>
    /// Realizes the transfer function as a State-Space model (A, B, C, D) in Controllable Canonical Form.
    /// Requires proper transfer function (deg(N) &lt;= deg(D)).
    /// </summary>
    public StateSpace ToStateSpace()
    {
        int n = Denominator.Degree;
        if (n == 0)
        {
            // Pure algebraic gain: y = (b0/a0) * u
            double dGain = Numerator.Evaluate(0.0) / Denominator.Evaluate(0.0);
            return new StateSpace(new double[0, 0], new double[0, 1], new double[1, 0], new double[,] { { dGain } });
        }

        // Align numerator coefficients to length n + 1
        double[] b = new double[n + 1];
        double[] a = new double[n + 1];

        // Denominator is already normalized so a[0] = 1.0 (highest degree)
        for (int i = 0; i <= n; i++)
        {
            a[i] = Denominator.Coefficients[i];
        }

        int numDeg = Numerator.Degree;
        int offset = n - numDeg;
        for (int i = 0; i <= numDeg; i++)
        {
            b[offset + i] = Numerator.Coefficients[i];
        }

        // D matrix is the direct feedthrough scalar: b[0]
        double d = b[0];

        // Controllable Canonical Form:
        // A matrix: size n x n
        double[,] A = new double[n, n];
        for (int i = 0; i < n - 1; i++)
        {
            A[i, i + 1] = 1.0;
        }
        for (int j = 0; j < n; j++)
        {
            A[n - 1, j] = -a[n - j];
        }

        // B matrix: size n x 1
        double[,] B = new double[n, 1];
        B[n - 1, 0] = 1.0;

        // C matrix: size 1 x n
        // C_j = b_{n-j} - a_{n-j} * d
        double[,] C = new double[1, n];
        for (int j = 0; j < n; j++)
        {
            C[0, j] = b[n - j] - a[n - j] * d;
        }

        double[,] D = new double[,] { { d } };

        return new StateSpace(A, B, C, D);
    }

    #region Operators

    public static TransferFunction operator +(TransferFunction a, TransferFunction b) => Parallel(a, b);
    public static TransferFunction operator -(TransferFunction a, TransferFunction b) => Parallel(a, -b);
    public static TransferFunction operator *(TransferFunction a, TransferFunction b) => Series(a, b);
    public static TransferFunction operator *(TransferFunction a, double scalar) => new(a.Numerator * scalar, a.Denominator);
    public static TransferFunction operator *(double scalar, TransferFunction a) => a * scalar;
    public static TransferFunction operator /(TransferFunction a, double scalar) => new(a.Numerator / scalar, a.Denominator);
    public static TransferFunction operator /(TransferFunction a, TransferFunction b) => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    public static TransferFunction operator -(TransferFunction a) => new(-a.Numerator, a.Denominator);

    #endregion

    public override string ToString()
    {
        return $"({Numerator}) / ({Denominator})";
    }

    public string ToDisplayString()
    {
        return $"{Numerator}\n───────\n{Denominator}";
    }

    public bool Equals(TransferFunction? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Numerator.Equals(other.Numerator) && Denominator.Equals(other.Denominator);
    }

    public override bool Equals(object? obj) => obj is TransferFunction tf && Equals(tf);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
}
