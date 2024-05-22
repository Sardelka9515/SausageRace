using Stride.Audio;
using Stride.Core.Mathematics;
using Stride.Engine;
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
        public void Run()
        {
            var power = -80 + (Rand.NextSingle() - 0.5f) * 20;
            FrontLeg.Get<RigidbodyComponent>().ApplyTorque(new(0, 0, power));
            RearLeg.Get<RigidbodyComponent>().ApplyTorque(new(0, 0, power));
        }
    }
    public class RaceScript : AsyncScript
    {
        // Declared public member fields and properties will show in the game studio

        List<Sausage> Sausages = [];
        List<Fork> Forks = [];
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

        Sound CountdownSound;
        Sound BGMSound;
        ColliderShape ForkCollider;
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
                    Camera.Get<CameraComponent>().OrthographicSize = 30;
                    if (EndCollider == null)
                    {
                        EndCollider = End.Get<StaticColliderComponent>();
                        EndCollider.Collisions.CollectionChanged += Collisions_CollectionChanged;
                        TB = (TextBlock)UIPage.RootElement.VisualChildren.First(c => c is TextBlock);
                        TB.Text = "";
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
                    float first = 0;

                    if (Racing)
                    {
                        foreach (var sausage in Sausages)
                        {
                            sausage.Run();
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
                    foreach (var fork in Forks)
                    {
                        if ((fork.Body.LinearVelocity + fork.Body.AngularVelocity).LengthSquared() < 0.1)
                        {
                            Entity.Scene.Entities.Remove(fork.Entity);
                        }
                    }
                    _elapsed += Game.UpdateTime.WarpElapsed.TotalSeconds;
                    while (_ticked * _tickInterval < _elapsed)
                    {
                        if (Racing && _ticked % 10 == 0 && Sausages.Any() && Sausage.Rand.Next(5) < 2)
                        {

                            var target = Sausages[Sausage.Rand.Next(Sausages.Count)];
                            var x = target.Body.Transform.Position.X;
                            SpawnFork(new(first + 3 + (Sausage.Rand.NextSingle() - 0.5f) * 20f, 50, Sausage.Rand.Next(0, Dist * Sausages.Count)));
                        }
                        _ticked++;
                        if (!Racing)
                        {
                            if (Winner != null)
                            {
                                Winner.BodyCollider.ApplyImpulse(new((End.Transform.Position.X - 5 - Winner.Body.Transform.Position.X) * 10, 0, 0));
                                if (Winner.Body.Transform.Position.Y < 10)
                                {
                                    Winner.BodyCollider.ApplyImpulse(new(0, 50, 0));
                                }
                            }
                            //if (bgm.PlayState == Stride.Media.PlayState.Playing && bgm.Volume > 0)
                            //{
                            //    bgm.Volume -= 0.01f;
                            //}
                            //if (bgm.Volume <= 0)
                            //{
                            //    bgm.Stop();
                            //}
                        }
                    }
                }
                await Script.NextFrame();
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
            Forks.Clear();
            Sausages.Clear();
            Camera.Get<CameraComponent>().OrthographicSize = 20;
            Camera.Transform.Position = new(0, 35, 45);
            Camera.Transform.RotationEulerXYZ = new(MathUtil.GradiansToRadians(-45), MathUtil.GradiansToRadians(10), 0);

            for (int i = 0; i < sausageCount; i++)
            {
                Sausages.Add(
                    SpawnSausage(new Vector3(0, 5, i * Dist),
                    ManMaterials[i % 4]));

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
