using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace DynamicIslandWin.Services;

/// <summary>
/// Authentic Apple/SwiftUI Damped Harmonic Oscillator Spring Easing Function for WPF.
/// Provides physical mass, elastic squish/stretch, and controlled overshoot.
/// Response: duration/stiffness (e.g. 0.48s). Damping: 0.68 for crisp iOS-like bounce.
/// </summary>
public class SpringEase : EasingFunctionBase
{
    public static readonly DependencyProperty ResponseProperty =
        DependencyProperty.Register(nameof(Response), typeof(double), typeof(SpringEase), new PropertyMetadata(0.48));

    public static readonly DependencyProperty DampingProperty =
        DependencyProperty.Register(nameof(Damping), typeof(double), typeof(SpringEase), new PropertyMetadata(0.68));

    public double Response
    {
        get => (double)GetValue(ResponseProperty);
        set => SetValue(ResponseProperty, value);
    }

    public double Damping
    {
        get => (double)GetValue(DampingProperty);
        set => SetValue(DampingProperty, value);
    }

    protected override double EaseInCore(double normalizedTime)
    {
        double t = normalizedTime;
        double zeta = Math.Clamp(Damping, 0.15, 0.98);
        double omega = (2.0 * Math.PI) / Math.Max(0.1, Response);
        double omegaD = omega * Math.Sqrt(1.0 - zeta * zeta);

        double decay = Math.Exp(-zeta * omega * t);
        double oscillation = Math.Cos(omegaD * t) + (zeta / Math.Sqrt(1.0 - zeta * zeta)) * Math.Sin(omegaD * t);

        return 1.0 - (decay * oscillation);
    }

    protected override Freezable CreateInstanceCore()
    {
        return new SpringEase { Response = Response, Damping = Damping };
    }
}
