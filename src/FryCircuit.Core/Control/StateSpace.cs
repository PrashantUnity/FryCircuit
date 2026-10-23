using System;
using System.Collections.Generic;

namespace FryCircuit.Core.Control;

/// <summary>
/// State-Space representation of a linear dynamical system:
/// dx/dt = A * x + B * u
///     y = C * x + D * u
/// Provides numerical integration for real-time simulation steps.
/// </summary>
public sealed class StateSpace
{
    public double[,] A { get; }
    public double[,] B { get; }
    public double[,] C { get; }
    public double[,] D { get; }

    public int StateCount => A.GetLength(0);
    public int InputCount => B.GetLength(1);
    public int OutputCount => C.GetLength(0);

    private double[] _state;
    private double _lastOutput;

    public IReadOnlyList<double> State => _state;
    public double LastOutput => _lastOutput;

    public StateSpace(double[,] a, double[,] b, double[,] c, double[,] d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        _state = new double[StateCount];
        _lastOutput = 0.0;
    }

    public void Reset()
    {
        Array.Clear(_state, 0, _state.Length);
        _lastOutput = 0.0;
    }

    public void SetStates(ReadOnlySpan<double> states)
    {
        int count = Math.Min(StateCount, states.Length);
        for (int i = 0; i < count; i++)
        {
            _state[i] = states[i];
        }
    }

    /// <summary>
    /// Computes instantaneous output y = C * x + D * u without modifying internal state.
    /// Safe to call repeatedly during Newton-Raphson MNA solver iterations.
    /// </summary>
    public double ComputeOutput(double u)
    {
        if (StateCount == 0)
        {
            _lastOutput = D.GetLength(0) > 0 && D.GetLength(1) > 0 ? D[0, 0] * u : 0.0;
            return _lastOutput;
        }

        double y = 0.0;
        for (int j = 0; j < StateCount; j++)
        {
            y += C[0, j] * _state[j];
        }
        if (D.GetLength(0) > 0 && D.GetLength(1) > 0)
        {
            y += D[0, 0] * u;
        }

        _lastOutput = y;
        return y;
    }

    /// <summary>
    /// Advances state by one timestep dt using 4th-Order Runge-Kutta (RK4) integration.
    /// Called once per converged simulation step in UpdateState.
    /// </summary>
    public void Advance(double u, double dt)
    {
        if (StateCount == 0 || dt <= 0.0) return;

        int n = StateCount;

        // k1 = A * x + B * u
        double[] k1 = ComputeDerivative(_state, u);

        // k2 = A * (x + 0.5 * dt * k1) + B * u
        double[] x2 = new double[n];
        for (int i = 0; i < n; i++) x2[i] = _state[i] + 0.5 * dt * k1[i];
        double[] k2 = ComputeDerivative(x2, u);

        // k3 = A * (x + 0.5 * dt * k2) + B * u
        double[] x3 = new double[n];
        for (int i = 0; i < n; i++) x3[i] = _state[i] + 0.5 * dt * k2[i];
        double[] k3 = ComputeDerivative(x3, u);

        // k4 = A * (x + dt * k3) + B * u
        double[] x4 = new double[n];
        for (int i = 0; i < n; i++) x4[i] = _state[i] + dt * k3[i];
        double[] k4 = ComputeDerivative(x4, u);

        // Next state: x = x + (dt / 6) * (k1 + 2*k2 + 2*k3 + k4)
        for (int i = 0; i < n; i++)
        {
            _state[i] += (dt / 6.0) * (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i]);
        }
    }

    /// <summary>
    /// Computes output and advances state in a single step (useful for standalone simulation loops).
    /// </summary>
    public double Step(double u, double dt)
    {
        double y = ComputeOutput(u);
        Advance(u, dt);
        return y;
    }

    private double[] ComputeDerivative(double[] x, double u)
    {
        int n = StateCount;
        double[] dxdt = new double[n];

        for (int i = 0; i < n; i++)
        {
            double sum = 0.0;
            for (int j = 0; j < n; j++)
            {
                sum += A[i, j] * x[j];
            }
            sum += B[i, 0] * u;
            dxdt[i] = sum;
        }

        return dxdt;
    }
}
