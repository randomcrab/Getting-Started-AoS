using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using Microsoft.Xna.Framework.Content;

namespace Game1
{
    public static class Data
    {
        public static GraphicsDeviceManager GDM;
        public static ContentManager CM;
        public static SpriteBatch SB;
        public static Game1 GAME;
    }

    public struct Physics
    {
        public float X, Y; //current position
        public float accX, accY; //accelerations
        public float preX, preY; //last position
    }

    public enum ParticleID : byte { Inactive, Fire, Rain }

    public struct Particle
    {   //an entity can be a struct too, w/ composition
        public Physics physics;
        public Color color;
        public float alpha;
        public float scale;
        public byte behavior;
        public ParticleID Id;
        public short age;
    }

    public static class ParticleSystem
    {   //max size depends on the platform, 30k is baseline/lowend
        public const int size = 30000;
        public static int active = 0;
        public static Particle[] data = new Particle[size];

        static Random Rand = new Random();
        public static Texture2D texture;
        public static Rectangle drawRec = new Rectangle(0, 0, 3, 3);
        public static float gravity = 0.1f;
        public static float windCounter = 0;

        public static void Reset()
        {   //acts as a constructor as well
            if (texture == null)
            {   //set up a general texture we can draw dots with, if required
                texture = new Texture2D(Data.GDM.GraphicsDevice, 1, 1);
                texture.SetData<Color>(new Color[] { Color.White });
            }
            //reset particles to inactive state
            for (int i = 0; i < size; i++)
            { data[i].Id = ParticleID.Inactive; }
            active = 0; //reset active count too
        }

        public static void Spawn(
            ParticleID ID,          //type of particle to spawn
            float X, float Y,       //spawn x, y position 
            float accX, float accY  //initial x, y acceleration
            )
        {   //create a stack instance to work on
            Particle P = new Particle();
            //setup P based on parameters
            P.physics.X = X; P.physics.preX = X;
            P.physics.Y = Y; P.physics.preY = Y;
            P.physics.accX = accX;
            P.physics.accY = accY;
            //set defaults for particle
            P.alpha = 1.0f;
            P.age = 0;
            P.behavior = 0;
            P.color = Color.White;
            P.scale = 1.0f;
            //pass the ID
            P.Id = ID;
            //setup particle based on ID
            if (P.Id == ParticleID.Fire)
            {   //fire is red, for instance
                P.color = Color.Red;
            }
            else if (P.Id == ParticleID.Rain)
            {   //rain is blue, this could be an animated sprite tho
                P.color = Color.Blue;
            }

            //find an inactive particle to copy data to
            int s = size;
            for (int i = 0; i < s; i++)
            {   //read each struct once
                if (data[i].Id == ParticleID.Inactive)
                {   //write local to heap once
                    data[i] = P;
                    active++;
                    return;
                }   //if all are active, 
            }       //fail silently/ignore
        }

        public static void Update()
        {
            //step 1 - update/animate all the particles
            //step 2 - randomly spawn fire and rain particles


            //increase the wind counter
            windCounter += 0.015f;
            //get left or right horizontal value for wind
            float wind = (float)Math.Sin(windCounter) * 0.03f;
            //store values for this heap data
            int width = Data.GDM.PreferredBackBufferWidth;
            int height = Data.GDM.PreferredBackBufferHeight;

            //step 1 - update all active particles
            int s = size;
            for (int i = 0; i < s; i++)
            {   //particle active, age + check id / behavior
                if (data[i].Id > 0)
                {
                    //create a local copy
                    Particle P = data[i];

                    //age active particles
                    P.age++;
                    //affect particles based on id
                    if (P.Id == ParticleID.Fire)
                    {   //fire rises, gravity does not affect it
                        P.physics.accY -= gravity;
                        P.physics.accY -= 0.05f;

                        //has longer life than rain
                        if (P.age > 300)
                        { P.Id = ParticleID.Inactive; }
                    }
                    else if (P.Id == ParticleID.Rain)
                    {   //rain falls (at different speeds)
                        P.physics.accY = Rand.Next(0, 100) * 0.001f;
                        //has shorter life than fire
                        if (P.age > 200)
                        { P.Id = ParticleID.Inactive; }
                    }

                    //add gravity to push down
                    P.physics.accY += gravity;
                    //add wind to push left/right
                    P.physics.accX += wind;

                    //calculate velocity using current and previous pos
                    float velocityX = P.physics.X - P.physics.preX;
                    float velocityY = P.physics.Y - P.physics.preY;

                    //store previous positions (the current positions)
                    P.physics.preX = P.physics.X;
                    P.physics.preY = P.physics.Y;

                    //set next positions using current + velocity + acceleration
                    P.physics.X = P.physics.X + velocityX + P.physics.accX;
                    P.physics.Y = P.physics.Y + velocityY + P.physics.accY;

                    //clear accelerations
                    P.physics.accX = 0; P.physics.accY = 0;

                    //cull particle if it goes off screen
                    if (P.physics.X < 0 || P.physics.X > width)
                    { P.Id = ParticleID.Inactive; }
                    else if (P.physics.Y < 0 || P.physics.Y > height)
                    { P.Id = ParticleID.Inactive; }

                    //decrement active value upon particle expiration
                    if (P.Id == ParticleID.Inactive) { active--; }

                    //write local to heap
                    data[i] = P;
                }
            }

            //step 2 - randomly spawn rain + fire
            for (int i = 0; i < 100; i++)
            {
                Spawn(ParticleID.Rain,
                    Rand.Next(0, width),
                    0, 0, 0);

                Spawn(ParticleID.Fire,
                    Rand.Next(0, width),
                    height - 0, 0, 0);
            }
        }

        public static void Draw()
        {   //open spritebatch and draw active particles
            Data.SB.Begin(SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.PointClamp);

            int s = size;
            for (int i = 0; i < s; i++)
            {   //if particle is active, draw it
                if (data[i].Id > 0)
                {   //create vector2 that draw wants
                    Vector2 pos = new Vector2(data[i].physics.X, data[i].physics.Y);
                    //draw each particle as a sprite
                    Data.SB.Draw(texture,
                        pos,
                        drawRec,
                        data[i].color * data[i].alpha,
                        0,
                        Vector2.Zero,
                        1.0f,
                        SpriteEffects.None,
                        i * 0.00001f);
                }   //layer particles based on index, as simple example
            }

            Data.SB.End();
        }
    }



    public class Game1 : Game
    {
        public static Stopwatch timer = new Stopwatch();
        public static long ticks = 0;

        public Game1()
        {
            //set data game refs
            Data.GDM = new GraphicsDeviceManager(this);
            Data.GDM.GraphicsProfile = GraphicsProfile.HiDef;
            Data.CM = Content;
            Data.GAME = this;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Data.GAME.Window.Title = "Monogame AoS Particle Example";
        }

        protected override void Initialize() { base.Initialize(); }

        protected override void LoadContent()
        {
            Data.SB = new SpriteBatch(GraphicsDevice);
            ParticleSystem.Reset(); //acts as constructor
        }

        protected override void UnloadContent() { } //lol

        protected override void Update(GameTime gameTime)
        {
            timer.Restart();
            base.Update(gameTime);
            ParticleSystem.Update();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            base.Draw(gameTime);
            ParticleSystem.Draw();
            timer.Stop();
            ticks = timer.ElapsedTicks;
            /*
            Console.WriteLine(
                "ticks:" + ticks + 
                " total: " + ParticleSystem.active +
                "/" + ParticleSystem.size);
            */
            Data.GAME.Window.Title = "AoS Particle System Example by @_mrgrak"
                + "- ticks per frame:" + ticks +
                " - total particles: " + ParticleSystem.active +
                "/" + ParticleSystem.size;
        }
    }
}