using System;
using System.Linq;
using System.Numerics;
using FryCircuit.Core.Control;
using Xunit;

namespace FryCircuit.Core.Tests;

public class PolynomialTests
{
    [Fact]
    public void AdditionAndSubtraction_WorkCorrectly()
    {
        // (s^2 + 3s + 2) + (2s + 5) = s^2 + 5s + 7
        var p1 = new Polynomial(1.0, 3.0, 2.0);
        var p2 = new Polynomial(2.0, 5.0);

        var sum = p1 + p2;
        Assert.Equal(2, sum.Degree);
        Assert.Equal(1.0, sum.Coefficients[0], 6);
        Assert.Equal(5.0, sum.Coefficients[1], 6);
        Assert.Equal(7.0, sum.Coefficients[2], 6);

        var diff = p1 - p2;
        Assert.Equal(1.0, diff.Coefficients[0], 6);
        Assert.Equal(1.0, diff.Coefficients[1], 6);
        Assert.Equal(-3.0, diff.Coefficients[2], 6);
    }

    [Fact]
    public void Multiplication_ConvolvesCoefficients()
    {
        // (s + 1) * (s + 2) = s^2 + 3s + 2
        var p1 = new Polynomial(1.0, 1.0);
        var p2 = new Polynomial(1.0, 2.0);

        var prod = p1 * p2;
        Assert.Equal(2, prod.Degree);
        Assert.Equal(1.0, prod.Coefficients[0], 6);
        Assert.Equal(3.0, prod.Coefficients[1], 6);
        Assert.Equal(2.0, prod.Coefficients[2], 6);
    }

    [Fact]
    public void HornerEvaluation_ComputesRealAndComplexValues()
    {
        // P(s) = s^2 + 6s + 100
        var p = new Polynomial(1.0, 6.0, 100.0);

        // At s = 0: P(0) = 100
        Assert.Equal(100.0, p.Evaluate(0.0), 6);

        // At s = 2: P(2) = 4 + 12 + 100 = 116
        Assert.Equal(116.0, p.Evaluate(2.0), 6);

        // At s = j * 10: P(j10) = -100 + j*60 + 100 = j*60
        Complex s = new Complex(0.0, 10.0);
        Complex val = p.Evaluate(s);
        Assert.Equal(0.0, val.Real, 6);
        Assert.Equal(60.0, val.Imaginary, 6);
    }

    [Fact]
    public void DerivativeAndIntegral_WorkCorrectly()
    {
        // P(s) = 3s^2 + 4s + 5
        var p = new Polynomial(3.0, 4.0, 5.0);

        // dP/ds = 6s + 4
        var d = p.Derivative();
        Assert.Equal(1, d.Degree);
        Assert.Equal(6.0, d.Coefficients[0], 6);
        Assert.Equal(4.0, d.Coefficients[1], 6);

        // Integral = s^3 + 2s^2 + 5s
        var intP = p.Integral();
        Assert.Equal(3, intP.Degree);
        Assert.Equal(1.0, intP.Coefficients[0], 6);
        Assert.Equal(2.0, intP.Coefficients[1], 6);
        Assert.Equal(5.0, intP.Coefficients[2], 6);
        Assert.Equal(0.0, intP.Coefficients[3], 6);
    }

    [Fact]
    public void FindRoots_SecondOrderComplexConjugate_MatchesAnalytical()
    {
        // s^2 + 6s + 100 = 0 => s = -3 +/- j*sqrt(91) ~= -3 +/- 9.53939j
        var p = new Polynomial(1.0, 6.0, 100.0);
        var roots = p.FindRoots();

        Assert.Equal(2, roots.Count);
        double expectedImag = Math.Sqrt(91.0);

        Assert.Contains(roots, r => Math.Abs(r.Real - (-3.0)) < 1e-4 && Math.Abs(Math.Abs(r.Imaginary) - expectedImag) < 1e-4);
    }

    [Fact]
    public void FindRoots_ThirdOrderTripleRoot()
    {
        // (s + 2)^3 = s^3 + 6s^2 + 12s + 8
        var p = new Polynomial(1.0, 6.0, 12.0, 8.0);
        var roots = p.FindRoots();

        Assert.Equal(3, roots.Count);
        foreach (var r in roots)
        {
            Assert.Equal(-2.0, r.Real, 1e-2);
            Assert.Equal(0.0, r.Imaginary, 1e-2);
        }
    }
}
