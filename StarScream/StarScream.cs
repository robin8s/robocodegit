
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
        //x1, y1 = position of the scanned bot
        double x1 = evt.X;
        double y1 = evt.Y;
        //x2, y2 = position of our bot
        double x2 = X;
        double y2 = Y;
        //differences in position
        double xt = x2 - x1;
        double yt = y2 - y1;
        //calculate angle of bot from y axis
        double angle = Math.Atan2(Math.Abs(xt), Math.Abs(yt));
        angle = angle * 180 / Math.PI;
        //relative angle is bots angular position in terms of circle plane, (0 = right)
        double RelativeAngle = 0;
        //detrmine if angle is in the first, second, third, or fourth quadrant and calculate relative angle accordingly
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
        //turn angle is how much to turnn
        double turnAngle = RelativeAngle - RadarDirection;
        //turn left or right depending on if turn angle is positive or negative
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
