using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;

public class StarScream : Bot
{
    private int _orbitDirection = 1;
    double lastVx = 0;
    double lastVy = 0;
    bool hasLastVelocity = false;

    // Wave surfing state
    private double _lastEnemyEnergy = 100;
    private double _surfDirection = 1;

    // Movement personality: true = center‑biased, false = edge‑biased
    private bool _preferCenter;

    private static readonly Random Random = new Random();

    // Bullet wave structure
    private class BulletWave
    {
        public double OriginX;
        public double OriginY;
        public double Speed;
        public double DistanceTraveled;
    }

    private readonly List<BulletWave> _waves = new List<BulletWave>();

    // Freeze‑prevention state
    private ScannedBotEvent _lastScan = null;
    private int _ticksSinceScan = 0;

    // Entry point
    static void Main(string[] args)
    {
        new StarScream().Start();
    }

    public override void Run()
    {
        AdjustRadarForBodyTurn = true;
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        // Choose movement personality at the start of the round
        _preferCenter = Random.NextDouble() < 0.5;

        TurnRadarLeft(double.PositiveInfinity);

        while (IsRunning)
            Go();
    }

    // TICK EVENT (used only when we are "blind")
    public override void OnTick(TickEvent e)
    {
        _ticksSinceScan++;

        // If we haven't scanned the enemy for a bit, keep radar and movement alive
        if (_ticksSinceScan > 5)
        {
            // Sweep radar to try to reacquire
            SetTurnRadarLeft(45);

            // Continue movement using last known scan so we don't freeze
            if (_lastScan != null)
            {
                CalculateMovement(_lastScan);
            }
        }
    }

    // SCANNED BOT EVENT
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Remember last scan and reset "blind" counter
        _lastScan = e;
        _ticksSinceScan = 0;

        // -------------------------
        // 1. RADAR LOCK (unchanged)
        // -------------------------
        double bearing = RadarBearingTo(e.X, e.Y);

        if (Math.Abs(bearing) > 45)
        {
            SetTurnRadarLeft(360);
            // DO NOT return; let the rest of the logic still run
        }

        double spread = Math.Atan(36.0 / DistanceTo(e.X, e.Y)) * (180.0 / Math.PI);
        double radarTurn = bearing + (bearing >= 0 ? spread : -spread);
        SetTurnRadarLeft(radarTurn);

        // -------------------------
        // 2. GUN LOGIC (unchanged)
        // -------------------------
        CalculateFiringSolution(e);

        // -------------------------
        // 3. WAVE SURFING: detect enemy fire
        // -------------------------
        double energyDrop = _lastEnemyEnergy - e.Energy;

        if (energyDrop > 0 && energyDrop <= 3.0)
        {
            BulletWave wave = new BulletWave()
            {
                OriginX = e.X,
                OriginY = e.Y,
                Speed = 20 - 3 * energyDrop,
                DistanceTraveled = 0
            };

            _waves.Add(wave);

            _surfDirection = -_surfDirection;
        }

        _lastEnemyEnergy = e.Energy;

        // -------------------------
        // 4. MOVEMENT
        // -------------------------
        CalculateMovement(e);

        // -------------------------
        // 5. UPDATE WAVES
        // -------------------------
        foreach (var wave in _waves)
            wave.DistanceTraveled += wave.Speed;

        _waves.RemoveAll(w =>
        {
            double dx = X - w.OriginX;
            double dy = Y - w.OriginY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            return dist < w.DistanceTraveled - 50;
        });
    }

    // ---------------------------------------------------------
    // GUN LOGIC (unchanged exactly as requested)
    // ---------------------------------------------------------
    private void CalculateFiringSolution(ScannedBotEvent e)
    {
        double bulletSpeed = CalcBulletSpeed(0.5);

        // Current enemy state
        double ex = e.X;
        double ey = e.Y;

        double vx = e.Speed * Math.Cos(e.Direction * Math.PI / 180.0);
        double vy = e.Speed * Math.Sin(e.Direction * Math.PI / 180.0);

        // Compute acceleration (difference from last tick)
        double ax = 0;
        double ay = 0;

        if (hasLastVelocity)
        {
            ax = vx - lastVx;
            ay = vy - lastVy;
        }

        lastVx = vx;
        lastVy = vy;
        hasLastVelocity = true;

        // Prediction variables
        double px = ex;
        double py = ey;

        double pvx = vx;
        double pvy = vy;

        double t = 0.0;
        double dt = 1.0;

        for (int i = 0; i < 200; i++)
        {
            // Distance from us to predicted point
            double dx = px - X;
            double dy = py - Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            double bulletTime = dist / bulletSpeed;

            // Converged
            if (Math.Abs(bulletTime - t) < 0.5)
                break;

            // Apply acceleration, but damp it to avoid wild overshoot
            pvx += ax * 0.5;
            pvy += ay * 0.5;

            // If enemy is slowing down, damp velocity heavily
            if (Math.Sign(pvx) != Math.Sign(vx)) pvx *= 0.5;
            if (Math.Sign(pvy) != Math.Sign(vy)) pvy *= 0.5;

            // Hard stop detection: if speed is tiny, clamp prediction
            double speed = Math.Sqrt(pvx * pvx + pvy * pvy);
            if (speed < 0.5)
            {
                pvx = 0;
                pvy = 0;
            }

            // Advance predicted position
            px += pvx * dt;
            py += pvy * dt;

            t += dt;
        }

        // Aim at predicted point
        double aimAngle = Math.Atan2(py - Y, px - X) * 180.0 / Math.PI;
        if (double.IsNaN(aimAngle))
            return;

        // Normalize turn
        double delta = aimAngle - GunDirection;
        delta = (delta + 540) % 360 - 180;

        if (delta > 0)
            SetTurnGunLeft(delta);
        else
            SetTurnGunRight(-delta);

        if (GunHeat == 0)
            SetFire(0.5);
    }

    // ---------------------------------------------------------
    // MOVEMENT SYSTEM (Mid‑range + Zero‑wall‑touch + Surfing)
    // ---------------------------------------------------------
    private void CalculateMovement(ScannedBotEvent e)
    {
        double dist = DistanceTo(e.X, e.Y);
        double angleToEnemy = DirectionTo(e.X, e.Y);

        // Mid‑range band
        double minDist = 350;
        double maxDist = 550;

        double moveAngle;

        // -----------------------------
        // 1. Distance control
        // -----------------------------
        if (dist < minDist)
        {
            // Too close → retreat
            moveAngle = angleToEnemy + 180;
        }
        else if (dist > maxDist)
        {
            // Too far → close in but offset to avoid head‑on
            moveAngle = angleToEnemy + (_surfDirection * 60);
        }
        else
        {
            // In ideal range → surf perpendicular
            moveAngle = angleToEnemy + (_surfDirection * 90);
        }

        // -----------------------------
        // 2. Personality bias
        // -----------------------------
        if (_preferCenter)
        {
            double centerAngle = DirectionTo(ArenaWidth / 2, ArenaHeight / 2);
            moveAngle = LerpAngle(moveAngle, centerAngle, 0.25);
        }

        // -----------------------------
        // 3. Apply wall‑safe correction
        // -----------------------------
        moveAngle = SafeAngle(moveAngle);

        // -----------------------------
        // 4. Execute movement
        // -----------------------------
        double turn = CalcDeltaAngle(moveAngle, Direction);
        SetTurnLeft(turn);
        SetForward(140);
    }

    // ---------------------------------------------------------
    // WALL‑SAFE ANGLE CORRECTION
    // ---------------------------------------------------------
    private double SafeAngle(double angle)
    {
        double rad = angle * Math.PI / 180.0;

        // Projected movement point
        double px = X + Math.Cos(rad) * 140;
        double py = Y + Math.Sin(rad) * 140;

        double margin = 40;   // soft margin
        double hard = 10;     // absolute minimum (currently unused, but kept)

        // Bend away from walls BEFORE moving
        if (px < margin)
            angle = Bend(angle, 0);          // push east
        else if (px > ArenaWidth - margin)
            angle = Bend(angle, 180);        // push west

        if (py < margin)
            angle = Bend(angle, 90);         // push north
        else if (py > ArenaHeight - margin)
            angle = Bend(angle, 270);        // push south

        return angle;
    }

    // ---------------------------------------------------------
    // ANGLE BENDING (smooth wall avoidance)
    // ---------------------------------------------------------
    private double Bend(double currentAngle, double wallNormal)
    {
        double delta = CalcDeltaAngle(wallNormal, currentAngle);

        // Push angle away from the wall by up to 70°
        double push = Math.Sign(delta) * 70;

        return currentAngle + push;
    }

    // ---------------------------------------------------------
    // ANGLE LERP (smooth personality blending)
    // ---------------------------------------------------------
    private double LerpAngle(double a, double b, double t)
    {
        double delta = CalcDeltaAngle(b, a);
        return a + delta * t;
    }
}
