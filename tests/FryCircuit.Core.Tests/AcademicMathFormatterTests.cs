using System;
using System.Collections.Generic;
using System.Numerics;
using FryCircuit.Core.Control;
using Xunit;

namespace FryCircuit.Core.Tests;

public class AcademicMathFormatterTests
{
    [Fact]
    public void GroupAndFormatRoots_FormatsRealRootsCleanlyWithoutImaginaryZero()
    {
        // Two purely real poles: -2.29 and -0.66
        var rawRoots = new List<Complex>
        {
            new Complex(-2.29, 0.0),
            new Complex(-0.66, 0.0)
        };

        var academic = AcademicMathFormatter.GroupAndFormatRoots(rawRoots, "s");

        Assert.Equal(2, academic.Count);
        // Dominant pole closest to jw-axis appears first
        Assert.Equal("s₁", academic[0].Label);
        Assert.Equal("-0.66", academic[0].ValueString);
        Assert.False(academic[0].IsComplexPair);
        Assert.False(academic[0].IsRhp);

        Assert.Equal("s₂", academic[1].Label);
        Assert.Equal("-2.29", academic[1].ValueString);
    }

    [Fact]
    public void GroupAndFormatRoots_GroupsConjugatePairsWithDampingAndWn()
    {
        // Complex conjugate pair: -0.03 - 1.15j and -0.03 + 1.15j
        var rawRoots = new List<Complex>
        {
            new Complex(-0.03, -1.15),
            new Complex(-0.03, 1.15),
            new Complex(-2.29, 0.0)
        };

        var academic = AcademicMathFormatter.GroupAndFormatRoots(rawRoots, "s");

        Assert.Equal(2, academic.Count);

        // Complex pair
        var pair = academic[0];
        Assert.Equal("s₁,₂", pair.Label);
        Assert.Contains("± j1.15", pair.ValueString);
        Assert.True(pair.IsComplexPair);
        Assert.NotNull(pair.DampingRatioZeta);
        Assert.NotNull(pair.NaturalFreqWn);
        Assert.True(pair.DampingRatioZeta.Value > 0.0);
        Assert.True(pair.NaturalFreqWn.Value > 1.0);
        Assert.Contains("ζ =", pair.DampingDetails);
        Assert.Contains("ωn =", pair.DampingDetails);

        // Real root
        Assert.Equal("s₃", academic[1].Label);
        Assert.Equal("-2.29", academic[1].ValueString);
    }

    [Fact]
    public void GroupAndFormatRoots_IdentifiesRhpUnstablePoles()
    {
        var rawRoots = new List<Complex>
        {
            new Complex(0.5, 2.0),
            new Complex(0.5, -2.0)
        };

        var academic = AcademicMathFormatter.GroupAndFormatRoots(rawRoots, "s");
        Assert.Single(academic);
        Assert.True(academic[0].IsRhp);
        Assert.Equal("s₁,₂", academic[0].Label);
        Assert.Equal("0.50 ± j2.00", academic[0].ValueString);
    }

    [Fact]
    public void ToAcademicPolynomial_ConvertsToUnicodeSuperscripts()
    {
        // P(s) = s^3 + 6s^2 + 11s + 6
        var poly = new Polynomial(1.0, 6.0, 11.0, 6.0);
        string academic = poly.ToAcademicString("s");

        Assert.Equal("s³ + 6s² + 11s + 6", academic);
    }

    [Fact]
    public void ToAcademicPolynomial_HandlesNegativeCoefficientsAndZeroTerms()
    {
        // P(s) = s^2 - 4
        var poly = new Polynomial(1.0, 0.0, -4.0);
        string academic = poly.ToAcademicString("s");

        Assert.Equal("s² - 4", academic);
    }

    [Fact]
    public void ToDiscretePolynomial_FormatsNegativeZPowers()
    {
        // H(z) = 0.5 + 0.3 z^-1 - 0.2 z^-2
        var coeffs = new double[] { 0.5, 0.3, -0.2 };
        string academic = AcademicMathFormatter.ToDiscretePolynomial(coeffs, "z");

        Assert.Equal("0.5 + 0.3z⁻¹ - 0.2z⁻²", academic);
    }

    [Fact]
    public void FormatSubscriptAndSuperscript_WorkForAllDigits()
    {
        Assert.Equal("₁₂", AcademicMathFormatter.FormatSubscript(12));
        Assert.Equal("³⁴", AcademicMathFormatter.FormatSuperscript(34));
    }
}
