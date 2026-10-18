namespace FryCircuit.Core.Physics;

/// <summary>
/// Exact fundamental physical and quantum constants (CODATA 2018 / SI definition standard).
/// Used for pico-precision semiconductor modeling, cryogenic quantum circuits, and space electronics.
/// </summary>
public static class PhysicalConstants
{
    /// <summary>Elementary charge in Coulombs (C): exact 1.602176634e-19 C</summary>
    public const double ElementaryCharge = 1.602176634e-19;

    /// <summary>Boltzmann constant in J/K: exact 1.380649e-23 J/K</summary>
    public const double BoltzmannConstant = 1.380649e-23;

    /// <summary>Planck constant in J*s: exact 6.62607015e-34 J*s</summary>
    public const double PlanckConstant = 6.62607015e-34;

    /// <summary>Reduced Planck constant (hbar = h / 2pi) in J*s</summary>
    public const double Hbar = 1.054571817e-34;

    /// <summary>Speed of light in vacuum in m/s: exact 299792458 m/s</summary>
    public const double SpeedOfLight = 299792458.0;

    /// <summary>Vacuum permittivity (epsilon_0) in F/m: ~8.8541878128e-12 F/m</summary>
    public const double VacuumPermittivity = 8.8541878128e-12;

    /// <summary>Vacuum permeability (mu_0) in H/m: ~1.25663706212e-6 H/m</summary>
    public const double VacuumPermeability = 1.25663706212e-6;

    /// <summary>Magnetic flux quantum Phi_0 = h / (2e) in Weber (Wb): ~2.067833848e-15 Wb</summary>
    public const double FluxQuantum = PlanckConstant / (2.0 * ElementaryCharge);

    /// <summary>Josephson constant K_J = 2e / h in Hz/V: ~483597.8484 GHz/V</summary>
    public const double JosephsonConstant = (2.0 * ElementaryCharge) / PlanckConstant;

    /// <summary>Von Klitzing quantum resistance standard R_K = h / e^2: ~25812.80745 Ohm</summary>
    public const double VonKlitzingConstant = PlanckConstant / (ElementaryCharge * ElementaryCharge);

    // Standard Aerospace and Quantum Operating Temperatures in Kelvin
    public const double TempQuantumCryogenic = 0.015;  // 15 mK (Dilution Refrigerator for Transmon Qubits)
    public const double TempLiquidHelium = 4.2;        // 4.2 K (Superconducting Circuits)
    public const double TempLiquidNitrogen = 77.36;    // 77.36 K (HTS Superconductors / Cryo-CMOS)
    public const double TempDeepSpace = 150.0;         // 150 K (-123°C Spacecraft shadow)
    public const double TempMilStdLow = 218.15;        // -55°C (MIL-STD-883 Aerospace minimum)
    public const double TempRoomStandard = 300.15;     // 27°C (Standard reference room temperature)
    public const double TempMilStdHigh = 398.15;       // +125°C (MIL-STD-883 Aerospace maximum)

    /// <summary>
    /// Computes the thermal voltage V_t = k_B * T / q for a given temperature in Kelvin.
    /// At 300.15 K, V_t ~ 25.86 mV. At 4.2 K, V_t ~ 0.36 mV.
    /// </summary>
    public static double ThermalVoltage(double tempKelvin = TempRoomStandard)
    {
        double t = Math.Max(0.001, tempKelvin);
        return (BoltzmannConstant * t) / ElementaryCharge;
    }

    /// <summary>
    /// Computes temperature-dependent silicon bandgap energy (Varshni formula).
    /// Eg(T) = Eg(0) - alpha * T^2 / (T + beta)
    /// </summary>
    public static double SiliconBandgap(double tempKelvin)
    {
        const double eg0 = 1.166;      // eV at 0K
        const double alpha = 4.73e-4;  // eV/K
        const double beta = 636.0;     // K
        double t = Math.Max(0.0, tempKelvin);
        return eg0 - (alpha * t * t) / (t + beta);
    }
}
