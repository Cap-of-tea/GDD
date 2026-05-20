namespace GDD.Core.Services;

public static class MouseMovementService
{
    public static List<(double X, double Y, int DelayMs)> GeneratePath(
        double fromX, double fromY, double toX, double toY)
    {
        var rng = Random.Shared;
        var totalDurationMs = rng.Next(500, 1501);
        var distance = Math.Sqrt(Math.Pow(toX - fromX, 2) + Math.Pow(toY - fromY, 2));

        if (distance < 5)
            return [(toX, toY, rng.Next(20, 60))];

        var steps = Math.Clamp((int)(distance / 8), 15, 60);
        var baseDelay = totalDurationMs / steps;

        var dx = toX - fromX;
        var dy = toY - fromY;
        var perpX = -dy / distance;
        var perpY = dx / distance;

        var spread1 = distance * (0.15 + rng.NextDouble() * 0.2) * (rng.Next(2) == 0 ? 1 : -1);
        var spread2 = distance * (0.10 + rng.NextDouble() * 0.15) * (rng.Next(2) == 0 ? 1 : -1);

        var cp1X = fromX + dx * 0.3 + perpX * spread1;
        var cp1Y = fromY + dy * 0.3 + perpY * spread1;
        var cp2X = fromX + dx * 0.7 + perpX * spread2;
        var cp2Y = fromY + dy * 0.7 + perpY * spread2;

        var points = new List<(double X, double Y, int DelayMs)>(steps);

        for (int i = 1; i <= steps; i++)
        {
            var t = EaseInOutCubic((double)i / steps);

            var bx = CubicBezier(t, fromX, cp1X, cp2X, toX);
            var by = CubicBezier(t, fromY, cp1Y, cp2Y, toY);

            if (i < steps)
            {
                bx += (rng.NextDouble() - 0.5) * 2.5;
                by += (rng.NextDouble() - 0.5) * 2.5;
            }

            var delay = Math.Max(baseDelay + rng.Next(-2, 3), 3);
            points.Add((Math.Round(bx, 1), Math.Round(by, 1), delay));
        }

        return points;
    }

    public static (double X, double Y) RandomStart(double targetX, double targetY, double viewportW = 393, double viewportH = 852)
    {
        var rng = Random.Shared;
        double startX, startY;
        do
        {
            startX = rng.Next(20, (int)viewportW - 20);
            startY = rng.Next(20, (int)viewportH - 20);
        } while (Math.Abs(startX - targetX) < 30 && Math.Abs(startY - targetY) < 30);

        return (startX, startY);
    }

    private static double CubicBezier(double t, double p0, double p1, double p2, double p3)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    private static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
}
