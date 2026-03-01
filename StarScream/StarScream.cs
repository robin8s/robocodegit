using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Collections.Generic;

public class StarScream : Bot
{
    private int _orbitDirection = 1;

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


    // SCANNED BOT EVENT
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // -------------------------
        // 1. RADAR LOCK (unchanged)
        // -------------------------
        double bearing = RadarBearingTo(e.X, e.Y);
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

        double dx = e.X - X;
        double dy = e.Y - Y;

        double vtx = e.Speed * Math.Cos(e.Direction * Math.PI / 180.0);
        double vty = e.Speed * Math.Sin(e.Direction * Math.PI / 180.0);

        double A = (vtx * vtx + vty * vty) - (bulletSpeed * bulletSpeed);
        double B = 2 * (dx * vtx + dy * vty);
        double C = dx * dx + dy * dy;

        double discriminant = B * B - 4 * A * C;
        if (discriminant < 0) return;

        double sqrtD = Math.Sqrt(discriminant);
        double t1 = (-B + sqrtD) / (2 * A);
        double t2 = (-B - sqrtD) / (2 * A);

        double t = double.MaxValue;
        if (t1 > 0 && t1 < t) t = t1;
        if (t2 > 0 && t2 < t) t = t2;
        if (t == double.MaxValue) return;

        double ux = (dx + vtx * t) / (bulletSpeed * t);
        double uy = (dy + vty * t) / (bulletSpeed * t);

        double aimAngle = Math.Atan2(uy, ux) * 180.0 / Math.PI;

        double delta = aimAngle - GunDirection;
        delta = (delta + 180) % 360;
        if (delta < 0) delta += 360;
        delta -= 180;

        if (delta > 0)
            SetTurnGunLeft(delta);
        else
            SetTurnGunRight(-delta);

        if (GunHeat == 0)
            SetFire(0.5);
    }


    // ---------------------------------------------------------
    // MOVEMENT SYSTEM (Wave Surfing Lite + Wall Avoidance)
    // ---------------------------------------------------------
    private void CalculateMovement(ScannedBotEvent e)
    {
        // HARD SAFETY: never get closer than 10 units to any wall
        if (IsTooCloseToWall(10))
        {
            EscapeWall();
            return;
        }

        // Find closest wave
        BulletWave closest = null;
        double closestDist = double.MaxValue;

        foreach (var wave in _waves)
        {
            double dx = X - wave.OriginX;
            double dy = Y - wave.OriginY;
            double dist = Math.Sqrt(dx * dx + dy * dy) - wave.DistanceTraveled;

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = wave;
            }
        }

        // No wave? fallback
        if (closest == null)
        {
            SimpleOrbit(e);
            return;
        }

        // Surf perpendicular to wave
        double angleFromWave = DirectionTo(closest.OriginX, closest.OriginY);
        double moveAngle = angleFromWave + (_surfDirection * 90);

        // Personality bias
        if (_preferCenter)
        {
            double centerAngle = DirectionTo(ArenaWidth / 2, ArenaHeight / 2);
            moveAngle = (moveAngle * 0.7) + (centerAngle * 0.3);
        }
        else
        {
            double edgeBias = DirectionTo(closest.OriginX, closest.OriginY) + 180;
            moveAngle = (moveAngle * 0.7) + (edgeBias * 0.3);
        }

        moveAngle = WallSmooth(moveAngle);

        double turn = CalcDeltaAngle(moveAngle, Direction);
        SetTurnLeft(turn);
        SetForward(120);
    }


    private void SimpleOrbit(ScannedBotEvent e)
    {
        double angleToEnemy = DirectionTo(e.X, e.Y);
        double orbitAngle = angleToEnemy + (_surfDirection * 90);

        if (_preferCenter)
        {
            double centerAngle = DirectionTo(ArenaWidth / 2, ArenaHeight / 2);
            orbitAngle = (orbitAngle * 0.7) + (centerAngle * 0.3);
        }

        orbitAngle = WallSmooth(orbitAngle);

        double turn = CalcDeltaAngle(orbitAngle, Direction);
        SetTurnLeft(turn);
        SetForward(120);
    }


    // ---------------------------------------------------------
    // WALL SAFETY
    // ---------------------------------------------------------
    private bool IsTooCloseToWall(double margin)
    {
        return
            X < margin ||
            X > ArenaWidth - margin ||
            Y < margin ||
            Y > ArenaHeight - margin;
    }

    private void EscapeWall()
    {
        double escapeAngle = Direction;

        if (X < 10) escapeAngle = 0;
        else if (X > ArenaWidth - 10) escapeAngle = 180;

        if (Y < 10) escapeAngle = 90;
        else if (Y > ArenaHeight - 10) escapeAngle = 270;

        double turn = CalcDeltaAngle(escapeAngle, Direction);
        SetTurnLeft(turn);
        SetForward(150);
    }


    // ---------------------------------------------------------
    // WALL SMOOTHING
    // ---------------------------------------------------------
    private double WallSmooth(double angle)
    {
        double stick = 140;
        double margin = 40;

        for (int i = 0; i < 20; i++)
        {
            double rad = angle * Math.PI / 180.0;
            double testX = X + Math.Cos(rad) * stick;
            double testY = Y + Math.Sin(rad) * stick;

            bool safe =
                testX > margin &&
                testX < ArenaWidth - margin &&
                testY > margin &&
                testY < ArenaHeight - margin;

            if (safe)
                return angle;

            angle += 5 * _surfDirection;
        }

        return angle;
    }
}
