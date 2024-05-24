using Stride.Audio;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Input;
using Stride.Physics;
using Stride.Rendering;
using Stride.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace SausageRace
{
    public class Fork
    {
        public Entity Entity;
        public RigidbodyComponent Body;
    }

    public class Meat
    {
        public Entity Entity;
        public RigidbodyComponent Body;
    }
    public class Sausage
    {
        public static Random Rand = new();
        public Entity Man;
        public Entity Body;
        public RigidbodyComponent BodyCollider;
        public HingeConstraint FrontHinge;
        public HingeConstraint RearHinge;
        public RigidbodyComponent FrontLegCollider;
        public RigidbodyComponent RearLegCollider;
        public RigidbodyComponent ManCollider;
        public Entity FrontLeg;
        public Entity RearLeg;
        public bool Boosting;
        public void Run()
        {
            var power = -80 + (Rand.NextSingle() - 0.5f) * 20;
            FrontLeg.Get<RigidbodyComponent>().ApplyTorqueImpulse(new(0, 0, power * 0.05f));
            RearLeg.Get<RigidbodyComponent>().ApplyTorqueImpulse(new(0, 0, power * 0.05f));
            if (Boosting)
            {
                BodyCollider.ApplyImpulse(new(50, 0, 0));
            }
        }
    }
    public class RaceScript : AsyncScript
    {
        // Declared public member fields and properties will show in the game studio

        List<Sausage> Sausages = [];
        List<Fork> Forks = [];
        List<Meat> Meats = [];
        Entity Camera;
        Entity End;
        StaticColliderComponent EndCollider;
        double _elapsed = 0;
        long _ticked = 0;
        double _tickInterval = 1 / (double)60;
        Material[] ManMaterials;
        bool Racing = false;
        const int Dist = 5;
        const int RaceLenth = 150;
        public Sausage Winner;
        public UIPage UIPage;
        public TextBlock TB;

        float first;
        Sound CountdownSound;
        Sound BGMSound;
        ColliderShape ForkCollider;
        ColliderShape MeatCollider;
        CameraComponent Cam;
        public override async Task Execute()
        {
            ManMaterials = [Content.Load<Material>("Black"), Content.Load<Material>("Blue"), Content.Load<Material>("Green"), Content.Load<Material>("Yellow")];
            CountdownSound = Content.Load<Sound>("Countdown");
            BGMSound = Content.Load<Sound>("BGM");
            SoundInstance cd = CountdownSound.CreateInstance();
            SoundInstance bgm = BGMSound.CreateInstance();
            while (Game.IsRunning)
            {
                Camera ??= Entity.Scene.Entities.FirstOrDefault(e => e.Name == "Camera");
                End ??= Entity.Scene.Entities.FirstOrDefault(e => e.Name == "End");
                UIPage ??= Entity.Scene.Entities.FirstOrDefault(e => e.Name == "Page").Get<UIComponent>().Page;
                if (Camera != null && End != null)
                {
                    ForkCollider = Entity.Scene.Entities.First(e => e.Name == "Fork").Get<RigidbodyComponent>().ColliderShape;
                    MeatCollider = Entity.Scene.Entities.First(e => e.Name == "Meat").Get<RigidbodyComponent>().ColliderShape;
                    Cam ??= Camera.Get<CameraComponent>();
                    Cam.OrthographicSize = 20;
                    Cam.VerticalFieldOfView = 35;
                    Cam.Projection = CameraProjectionMode.Perspective;
                    if (EndCollider == null)
                    {
                        EndCollider = End.Get<StaticColliderComponent>();
                        EndCollider.Collisions.CollectionChanged += Collisions_CollectionChanged;
                        TB = (TextBlock)UIPage.RootElement.VisualChildren.First(c => c is TextBlock);
                        TB.Text = "";
                        this.GetSimulation().PreTick += FixedUpdate;
                    }
                    if (Input.IsKeyPressed(Keys.Space))
                    {
                        Winner = null;
                        End.Transform.Position = new(RaceLenth, 2, 10);
                        EndCollider.UpdatePhysicsTransformation();
                        StartRace(4);
                        Racing = false;
                        bgm.Stop();
                        Camera.Transform.Position.X = 6;
                        Game.UpdateTime.Factor = 0;
                        await Script.NextFrame();
                        TB.Text = "Ready";
                        while (!Input.IsKeyPressed(Keys.Space)) { await Script.NextFrame(); }

                        bgm.Volume = 3;
                        for (int i = 0; i < 3; i++)
                        {
                            cd.Stop();
                            cd.Play();
                            TB.Text = (3 - i).ToString();
                            await Script.Wait(new(0, 0, 1));
                        }
                        bgm.Stop();
                        bgm.IsLooping = true;
                        bgm.Play();
                        cd.Play();
                        TB.Text = "";
                        Game.UpdateTime.Factor = 1;
                        Racing = true;
                    }
                    // Do stuff every new frame
                    first = 0;

                    if (Racing)
                    {
                        foreach (var sausage in Sausages)
                        {
                            var x = sausage.Body.Transform.Position.X;
                            if (x > first)
                            {
                                first = x;
                            }
                            // sausage.Man.Transform.UpdateWorldMatrix();
                            // sausage.ManCollider.UpdatePhysicsTransformation();
                        }
                        Camera.Transform.Position.X = first;
                    }
                    //foreach (var fork in Forks)
                    //{
                    //    if ((fork.Body.LinearVelocity + fork.Body.AngularVelocity).LengthSquared() < 0.1)
                    //    {
                    //        Entity.Scene.Entities.Remove(fork.Entity);
                    //    }
                    //}
                    _elapsed += Game.UpdateTime.WarpElapsed.TotalSeconds;
                    while (_ticked * _tickInterval < _elapsed)
                    {
                        _ticked++;
                    }
                    if (Winner != null && !Racing)
                    {
                        Camera.Transform.Rotation = Quaternion.BetweenDirections(Vector3.UnitZ, Camera.Transform.Position - Winner.Body.Transform.Position);
                    }
                }
                await Script.NextFrame();
            }
        }

        private void FixedUpdate(Simulation sender, float tick)
        {
            foreach (var sausage in Sausages)
            {
                sausage.Run();
            }
            if (Sausage.Rand.Next(0, 200) == 0)
            {
                var ordered = Sausages.OrderByDescending(s => s.Body.Transform.Position.X)
                    .Where(s => s.Body.Transform.WorldMatrix.Up.Y > 0.5f);
                if (ordered.Any())
                {
                    var toBoost = ordered.Last();
                    toBoost.Boosting = true;
                    Script.Scheduler.Add(async () =>
                    {
                        await Script.Wait(new(0, 0, 3));
                        toBoost.Boosting = false;
                    });
                }
            }
            if (Racing && _ticked % (int)float.Lerp(30, 5, first / End.Transform.Position.X) == 0 && Sausages.Any() && Sausage.Rand.Next(5) < 2)
            {

                var target = Sausages[Sausage.Rand.Next(Sausages.Count)];
                var x = target.Body.Transform.Position.X;
                SpawnFork(new(first + 3 + (Sausage.Rand.NextSingle() - 0.5f) * 20f, 50, Sausage.Rand.Next(0, Dist * Sausages.Count)));
            }
            if (!Racing)
            {
                if (Winner != null)
                {
                    Winner.BodyCollider.ApplyImpulse(new((End.Transform.Position.X - 5 - Winner.Body.Transform.Position.X) * 10, 0, 0));
                    if (Winner.Body.Transform.Position.Y < 10)
                    {
                        Winner.BodyCollider.ApplyImpulse(new(0, 50, 0));
                    }

                    foreach (var m in Meats)
                    {
                        var target = Winner.Body.Transform.Position + Vector3.UnitY * 10;
                        var f = target - m.Entity.Transform.Position;
                        //if (f.LengthSquared() < 10)
                        //{
                        //    continue;
                        //}
                        //f += new Vector3(Sausage.Rand.Next(-5, 5), Sausage.Rand.Next(-5, 5), Sausage.Rand.Next(-5, 5));

                        var min = Vector3.One * -10;
                        var max = Vector3.One * 10;
                        Vector3.Clamp(ref f, ref min, ref max, out var f2);
                        m.Body.ApplyImpulse(f2);
                    }
                }
            }
        }

        private void Collisions_CollectionChanged(object sender, Stride.Core.Collections.TrackingCollectionChangedEventArgs e)
        {
            if (Winner != null)
            {
                return;
            }
            var collision = (Collision)e.Item;
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                PhysicsComponent col = null;
                if (collision.ColliderA == EndCollider)
                {
                    col = collision.ColliderB;
                }
                else if (collision.ColliderB == EndCollider)
                {
                    col = collision.ColliderA;
                }
                if (col != null)
                {
                    foreach (var s in Sausages)
                    {
                        if (s.BodyCollider == col || s.FrontLegCollider == col || s.RearLegCollider == col)
                        {
                            Racing = false;
                            Winner = s;
                            foreach (var s2 in Sausages)
                            {
                                if (s2 != Winner)
                                {
                                    this.GetSimulation().RemoveConstraint(s2.FrontHinge);
                                    this.GetSimulation().RemoveConstraint(s2.RearHinge);
                                    const int range = 1000;
                                    s2.BodyCollider.ApplyImpulse(new(-5000f, Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
                                    s2.FrontLegCollider.ApplyImpulse(new(Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
                                    s2.RearLegCollider.ApplyImpulse(new(Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }



        public Meat SpawnMeat(Vector3 pos)
        {
            var model = Content.Load<Model>("MeatModel");
            var body = new RigidbodyComponent()
            {
                ColliderShape = MeatCollider,
                Restitution = 0.5f,
                Mass = 20
            };
            Entity e = new()
            {
                new ModelComponent(model),
                body
            };
            e.Transform.Position = pos;
            Entity.Scene.Entities.Add(e);
            body.UpdatePhysicsTransformation();
            const int range = 10;
            body.ApplyImpulse(new(Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
            body.ApplyTorqueImpulse(new(Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
            return new()
            {
                Entity = e,
                Body = body
            };
        }
        public Entity SpawnFork(Vector3 pos)
        {
            var forkModel = Content.Load<Model>("ForkModel");
            var forkBody = new RigidbodyComponent()
            {
                ColliderShape = ForkCollider,
                Restitution = 0.5f,
                Mass = 20
            };
            Entity e = new()
            {
                new ModelComponent(forkModel),
                forkBody
            };
            e.Transform.Position = pos;
            e.Transform.RotationEulerXYZ = new(MathUtil.PiOverTwo, MathUtil.Pi, 0);
            Entity.Scene.Entities.Add(e);
            forkBody.UpdatePhysicsTransformation();
            forkBody.ApplyImpulse(new(0, -1000, 0));
            Forks.Add(new()
            {
                Entity = e,
                Body = forkBody
            });
            return e;
        }

        public void StartRace(int sausageCount = 4)
        {
            var es = Entity.Scene.Entities;
            foreach (Sausage sausage in Sausages)
            {
                es.Remove(sausage.Body);
                es.Remove(sausage.FrontLeg);
                es.Remove(sausage.RearLeg);
                // es.Remove(sausage.Man);
            }
            foreach (var f in Forks)
            {
                es.Remove(f.Entity);
            }
            foreach (var f in Meats)
            {
                es.Remove(f.Entity);
            }
            Forks.Clear();
            Meats.Clear();
            Sausages.Clear();
            Cam.OrthographicSize = 20;
            Camera.Transform.Position = new(0, 35, 45);
            Camera.Transform.RotationEulerXYZ = new(MathUtil.GradiansToRadians(-45), MathUtil.GradiansToRadians(10), 0);

            for (int i = 0; i < sausageCount; i++)
            {
                Sausages.Add(
                    SpawnSausage(new Vector3(0, 5, i * Dist),
                    ManMaterials[i % 4]));
            }

            for (int i = 0; i < 50; i++)
            {
                var pos = new Vector3(Sausage.Rand.Next((int)End.Transform.Position.X / 2, (int)End.Transform.Position.X), 55, Sausage.Rand.Next(0, sausageCount * Dist));
                Meats.Add(SpawnMeat(pos));
            }
        }
        public Sausage SpawnSausage(Vector3 origin, Material manMat)
        {
            var bodyModel = Content.Load<Model>("SausageBody");
            var legModel = Content.Load<Model>("SausageLeg");
            var manModel = Content.Load<Model>("ManModel");

            var bodyCollider = new RigidbodyComponent()
            {
                ColliderShape = new CapsuleColliderShape(true, 0.7f, 4f, ShapeOrientation.UpY),
                Restitution = 0.5f,
                Mass = 80,
                CollisionGroup = CollisionFilterGroups.DefaultFilter,
                CanCollideWith = CollisionFilterGroupFlags.DefaultFilter
            };
            var legCollider = new RigidbodyComponent()
            {
                ColliderShape = new CapsuleColliderShape(true, 0.6f, 0.35f, ShapeOrientation.UpY),
                Restitution = 0.5f,
                Mass = 10,
                CollisionGroup = CollisionFilterGroups.CustomFilter1,
                CanCollideWith = CollisionFilterGroupFlags.DefaultFilter
            };
            var legCollider2 = new RigidbodyComponent()
            {
                ColliderShape = new CapsuleColliderShape(true, 0.6f, 0.35f, ShapeOrientation.UpY),
                Restitution = 0.5f,
                Mass = 10,
                CollisionGroup = CollisionFilterGroups.CustomFilter1,
                CanCollideWith = CollisionFilterGroupFlags.DefaultFilter
            };

            var manCollider = new RigidbodyComponent()
            {
                ColliderShape = Entity.Scene.Entities.First(e => e.Name == "man").Get<RigidbodyComponent>().ColliderShape,
                Restitution = 0.5f,
                Mass = 10,
                CollisionGroup = CollisionFilterGroups.CustomFilter1,
                CanCollideWith = CollisionFilterGroupFlags.DefaultFilter
            };
            var body = new Entity
                {
                    new ModelComponent(bodyModel),
                    bodyCollider,
                };
            var frontLeg = new Entity
                {
                    new ModelComponent(legModel),
                    legCollider,
                };
            var rearLeg = new Entity
                {
                    new ModelComponent(legModel),
                    legCollider2,
                };
            var manComp = new ModelComponent(manModel);
            manComp.Materials.Add(new(0, manMat));
            var man = new Entity()
                {
                    manComp,
                    // manCollider
                };
            man.Transform.Parent = body.Transform;
            man.Transform.RotationEulerXYZ = new(0, MathUtil.PiOverTwo, MathUtil.PiOverTwo);

            body.Transform.Position = origin;
            body.Transform.Rotation = Quaternion.RotationZ(-MathF.PI / 2);
            var fLegOffset = new Vector3(1f, 1.5f, 0f);
            var rLegOffset = new Vector3(1f, -1.5f, 0f);
            frontLeg.Transform.Position = origin + body.Transform.Rotation * fLegOffset;
            rearLeg.Transform.Position = origin + body.Transform.Rotation * rLegOffset;

            Entity.Scene.Entities.Add(body);
            // Entity.Scene.Entities.Add(man);
            Entity.Scene.Entities.Add(frontLeg);
            Entity.Scene.Entities.Add(rearLeg);

            bodyCollider.UpdatePhysicsTransformation();
            legCollider.UpdatePhysicsTransformation();
            legCollider2.UpdatePhysicsTransformation();
            // manCollider.UpdatePhysicsTransformation();

            var frontHinge = Simulation.CreateHingeConstraint(
                bodyCollider, fLegOffset, Vector3.UnitZ,
                legCollider, Vector3.UnitY * 0.2f, Vector3.UnitZ);
            this.GetSimulation().AddConstraint(frontHinge);
            Game.UpdateTime.Factor = 1f;
            var rearHinge = Simulation.CreateHingeConstraint(
                bodyCollider, rLegOffset, Vector3.UnitZ,
                legCollider2, Vector3.UnitY * 0.2f, Vector3.UnitZ);
            this.GetSimulation().AddConstraint(rearHinge);
            return new()
            {
                Body = body,
                BodyCollider = bodyCollider,
                FrontLeg = frontLeg,
                RearLeg = rearLeg,
                FrontHinge = frontHinge,
                RearHinge = rearHinge,
                FrontLegCollider = legCollider,
                RearLegCollider = legCollider2,
                ManCollider = manCollider,
                Man = man
            };
        }
    }
}
