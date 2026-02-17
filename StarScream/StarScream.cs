
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using System;
using System.Xml.Linq;

// ------------------------------------------------------------------
// MyFirstBot
// ------------------------------------------------------------------
// A sample bot originally made for Robocode by Mathew Nelson.
//
// Probably the first bot you will learn about.
// Moves in a seesaw motion and spins the gun around at each end.
// ------------------------------------------------------------------
public class StarScream : Bot
{
    // The main method starts our bot
    static void Main(string[] args)
    {
        new StarScream().Start();
    }

    // Called when a new round is started -> initialize and do some movement
    public override void Run()
    {
        // Repeat while the bot is running
        while (IsRunning)
        {
            //Forward(100);
            TurnRadarLeft(10);
           
           // Back(100);
            //TurnGunLeft(360);
        }
    }

    // We saw another bot -> fire!
    public override void OnScannedBot(ScannedBotEvent evt)
    {
        double x1 = evt.X;
        double y1 = evt.Y;
        double x2 = X;
        double y2 = Y;
        double xt = x2 - x1;
        double yt = y2 - y1;
        double angle = Math.Atan2(Math.Abs(xt), Math.Abs(yt));
        angle = angle * 180 / Math.PI;
        double RelativeAngle = 0;
        if (yt>0)
        {
            if (xt > 0)
            {
                RelativeAngle = 270 - angle;
            }
            if (xt < 0)
            {
                RelativeAngle = 270 + angle;
            }
        }
        if (yt < 0)
        {
            if (xt > 0)
            {
                RelativeAngle = 90 + angle;
            }
            if (xt < 0)
            {
                RelativeAngle = 90 - angle;
            }
        }
        double turnAngle = RelativeAngle - RadarDirection;
       if (turnAngle > 0)
        {
            TurnRadarRight(turnAngle);
        }
       if (turnAngle < 0)
        {
            TurnRadarLeft(turnAngle);
        }
        Fire(1);
    }

    // We were hit by a bullet -> turn perpendicular to the bullet
    public override void OnHitByBullet(HitByBulletEvent evt)
    {
        // Calculate the bearing to the direction of the bullet
        //var bearing = CalcBearing(evt.Bullet.Direction);

        // Turn 90 degrees to the bullet direction based on the bearing
        //TurnRight(90 - bearing);
    }
}
