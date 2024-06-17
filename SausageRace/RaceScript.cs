using Stride.Audio;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Graphics;
using Stride.Input;
using Stride.Particles;
using Stride.Particles.Components;
using Stride.Physics;
using Stride.Profiling;
using Stride.Rendering;
using Stride.UI.Controls;
using Stride.UI.Panels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Stride.UI;

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
        public int Id;
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
        public void Run(DebugTextSystem d)
        {
            const float maxSpeed = 25;
            var power = -80 + (Rand.NextSingle() - 0.5f) * 20;
            if (FrontLegCollider.AngularVelocity.Z > -maxSpeed)
            {
                FrontLegCollider.ApplyTorqueImpulse(new(0, 0, power * 0.05f));

            }
            if (RearLegCollider.AngularVelocity.Z > -maxSpeed)
            {
                RearLegCollider.ApplyTorqueImpulse(new(0, 0, power * 0.05f));
            }
            if (Boosting)
            {
                BodyCollider.ApplyImpulse(new(5, 0, 0));
            }

            var deg = MathUtil.RadiansToDegrees(Body.Transform.RotationEulerXYZ.Z);
            if (deg < -120)
            {
                BodyCollider.ApplyTorqueImpulse(new(0, 0, 10));
            }
            else if (deg > -60)
            {
                BodyCollider.ApplyTorqueImpulse(new(0, 0, -10));
            }
            // d.Print(deg.ToString(), new(10, Id * 10));
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
        const int RaceLenth = 300;
        Sausage Winner;
        UIPage UIPage;
        TextBlock TB;

        float first;
        const int SausageCount = 4;
        Sound CountdownSound;
        Sound BGMSound;
        ColliderShape ForkCollider;
        ColliderShape MeatCollider;
        CameraComponent Cam;
        SoundInstance cd;
        SoundInstance bgm;
        Prefab BoostVfx;
        Prefab ExplosionVfx;
        Dictionary<ColliderShape, Action<PhysicsComponent, Collision>> CollisionHandlers = [];
        Entity Ground;

        static Int2[] Resolutions =
            [new(3840, 2160), new(1920, 1080), new(1280, 720), new(1024, 480), new(800, 600), new(640, 480)];
        StackPanel ResPanel;
        public async Task Start()
        {
            Game.Window.AllowUserResizing = true;
            EndCollider = End.Get<StaticColliderComponent>();
            EndCollider.Collisions.CollectionChanged += Collisions_CollectionChanged;
            var elements = UIPage.RootElement.VisualChildren;
            TB = (TextBlock)elements.First(c => c is TextBlock);
            TB.Text = "";
            var grid = (Grid)elements[0];
            ResPanel = (StackPanel)grid.VisualChildren[0];
            foreach (var res in Resolutions)
            {
                var bt = new Button()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                var text = new TextBlock
                {
                    Text = $"{res.X}x{res.Y}",
                    Visibility = Visibility.Visible,
                    Opacity = 1,
                    TextColor = Color.White,
                    BackgroundColor = Color.Black,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Font = TB.Font,
                    TextSize = 20,
                    Margin = default,
                };
                bt.Click += (s, e) =>
                {
                    var fs = Game.Window.IsFullscreen;
                    Game.Window.IsFullscreen = false;
                    Game.Window.PreferredFullscreenSize = res;
                    Game.Window.PreferredWindowedSize = res;
                    Game.Window.SetSize(res);
                    Game.Window.IsFullscreen = fs;
                };
                bt.Content = text;
                ResPanel.Children.Add(bt);
            }
            AddMainButton();
            this.GetSimulation().PreTick += FixedUpdate;

            Ground = Entity.Scene.Entities.First(c => c.Name == "Ground");

            BoostVfx = await Content.LoadAsync<Prefab>("VFXPrefabs/vfx-Boost");
            ExplosionVfx = await Content.LoadAsync<Prefab>("VFXPrefabs/vfx-Explosion");
            CollisionHandlers.Add(EndCollider.ColliderShape, SausageWin);
            CollisionHandlers.Add(Ground.Get<StaticColliderComponent>().ColliderShape, ForkContact);
        }
        void AddMainButton()
        {

            var bt = new Button()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var text = new TextBlock
            {
                Text = $"Start/Stop",
                Visibility = Visibility.Visible,
                Opacity = 1,
                TextColor = Color.White,
                BackgroundColor = Color.Black,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Font = TB.Font,
                TextSize = 20,
                Margin = default,
            };
            bt.Click += (s, e) =>
            {
                touched = true;
            };
            bt.Content = text;
            ResPanel.Children.Add(bt);
        }
        void ForkContact(PhysicsComponent fork, Collision col)
        {
            var c = col.Contacts.First();

            Matrix.Translation(ref c.PositionOnA, out var posA);
            var point = (posA * c.ColliderA.PhysicsWorldTransform).Decompose(out Vector3 scale, out Vector3 worldPos);
            Matrix.Translation(ref worldPos, out var worldMat);
            SpawnInstance(ExplosionVfx, null, worldMat, 5);
        }
        void SausageWin(PhysicsComponent col, Collision c)
        {
            if (Winner != null)
            {
                return;
            }
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
        public override async Task Execute()
        {
            ManMaterials = [Content.Load<Material>("Black"), Content.Load<Material>("Blue"), Content.Load<Material>("Green"), Content.Load<Material>("Yellow")];
            CountdownSound = Content.Load<Sound>("Countdown");
            BGMSound = Content.Load<Sound>("BGM");
            cd = CountdownSound?.CreateInstance();
            bgm = BGMSound?.CreateInstance();
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
                        await Start();
                    }
                    if (MainControl())
                    {
                        await StartRace(SausageCount);
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
                        LookPosition(Camera, Winner.Body.Transform.Position);
                    }
                }
                if (!startBoost)
                {
                    startBoost = MainControl();
                    //Sausage.Rand.Next(0, 200) == 0
                }
                await Script.NextFrame();
            }
        }
        bool startBoost;
        public static void LookPosition(Entity e, Vector3 target, bool reverse = true)
        {
            e.Transform.Rotation = Quaternion.BetweenDirections(Vector3.UnitZ, e.Transform.Position - target);
        }
        private void FixedUpdate(Simulation sender, float tick)
        {
            startBoost = Sausage.Rand.Next(0, 100) == 0;
            foreach (var sausage in Sausages)
            {
                sausage.Run(DebugText);
            }
            if (startBoost)
            {
                startBoost = false;
                var ordered = Sausages.OrderByDescending(s => s.Body.Transform.Position.X)
                    .Where(s => (s.Body.Transform.Rotation * -Vector3.UnitX).Y > 0.1f &&
                    first - s.Body.Transform.Position.X > 10).ToArray();
                if (ordered.Length != 0)
                {
                    Boost(ordered[Sausage.Rand.Next(0, ordered.Length)], 1f);
                }
            }
            if (Racing && _ticked % (int)float.Lerp(60, 20, first / End.Transform.Position.X) == 0 && Sausages.Any() && Sausage.Rand.Next(5) < 2)
            {

                var target = Sausages[Sausage.Rand.Next(Sausages.Count)];
                var x = target.Body.Transform.Position.X;
                SpawnFork(new(first + 8 + (Sausage.Rand.NextSingle() - 0.5f) * 20f, 50, Sausage.Rand.Next(0, Dist * Sausages.Count)));
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

        bool touched = true;
        bool MainControl()
        {
            if (touched)
            {
                touched = false;
                return true;
            }
            return Input.IsKeyPressed(Keys.Space);
        }
        private void Collisions_CollectionChanged(object sender, Stride.Core.Collections.TrackingCollectionChangedEventArgs e)
        {
            var collision = (Collision)e.Item;
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (CollisionHandlers.TryGetValue(collision.ColliderA.ColliderShape, out var action))
                {
                    action(collision.ColliderB, collision);
                }
                else if (CollisionHandlers.TryGetValue(collision.ColliderB.ColliderShape, out var action2))
                {
                    action2(collision.ColliderA, collision);
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
                Mass = 50
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
            var range = 20;
            forkBody.ApplyTorqueImpulse(new(Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range), Sausage.Rand.Next(-range, range)));
            Forks.Add(new()
            {
                Entity = e,
                Body = forkBody
            });
            return e;
        }

        public async Task StartRace(int sausageCount = 4)
        {
            Winner = null;
            End.Transform.Position = new(RaceLenth, 2, 10);
            EndCollider.UpdatePhysicsTransformation();
            Racing = false;
            bgm.Stop();
            Camera.Transform.Position.X = 6;
            Game.UpdateTime.Factor = 0;
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

            for (int i = 0; i < sausageCount; i++)
            {
                var s = SpawnSausage(new Vector3(0, 2, i * Dist), ManMaterials[i % 4]);
                s.Id = i;
                Sausages.Add(s);
            }

            for (int i = 0; i < 50; i++)
            {
                var pos = new Vector3(Sausage.Rand.Next((int)End.Transform.Position.X / 2, (int)End.Transform.Position.X), 55, Sausage.Rand.Next(0, sausageCount * Dist));
                Meats.Add(SpawnMeat(pos));
            }

            TB.Text = "Ready";
            await Script.NextFrame();
            int cycle = 0;
            Vector3 start, end;
            start = new Vector3(3, 8, 7);
        animStart:
            end = start;
            end.Z += (sausageCount - 1) * Dist;
            Camera.Transform.Position = start;
            LookPosition(Camera, Sausages[0].Man.Transform.Position + Vector3.UnitY * 3);
            double interpolationTime = sausageCount * 1.5;
            double interpolated = 0;
            while (!MainControl())
            {
                // Camera animation
                interpolated += Game.UpdateTime.Elapsed.TotalSeconds;
                if (interpolated > interpolationTime)
                {
                    cycle++;
                    interpolated = 0;
                    if (cycle % 2 == 0)
                    {
                        start = new Vector3(3, 8, 7);
                    }
                    else
                    {
                        start = new Vector3(-3, 8, -7);
                    }
                    goto animStart;
                }
                Camera.Transform.Position = Vector3.Lerp(start, end, (float)(interpolated / interpolationTime));
                Cam.Update();
                await Script.NextFrame();
            }

            Camera.Transform.Position = new(0, 35, 45);
            Camera.Transform.RotationEulerXYZ = new(MathUtil.GradiansToRadians(-45), MathUtil.GradiansToRadians(10), 0);

            if (bgm != null)
            {
                bgm.Volume = 3;
            }
            for (int i = 0; i < 3; i++)
            {
                cd?.Stop();
                cd?.Play();
                TB.Text = (3 - i).ToString();
                await Script.Wait(1);
            }
            if (bgm != null)
            {
                bgm.Stop();
                bgm.IsLooping = true;
                bgm.Play();
            }
            cd?.Play();
            TB.Text = "";
            Game.UpdateTime.Factor = 1;
            Racing = true;
            foreach (var s in Sausages)
            {
                Boost(s);
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
                Friction = 1,
                CollisionGroup = CollisionFilterGroups.CustomFilter1,
                CanCollideWith = CollisionFilterGroupFlags.DefaultFilter
            };
            var legCollider2 = new RigidbodyComponent()
            {
                ColliderShape = new CapsuleColliderShape(true, 0.6f, 0.35f, ShapeOrientation.UpY),
                Restitution = 0.5f,
                Mass = 10,
                Friction = 1,
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
            var fLegOffset = new Vector3(0.7f, 1.6f, 0f);
            var rLegOffset = new Vector3(0.7f, -1.6f, 0f);
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

        public void Boost(Sausage sausage, float duration = 3)
        {
            var scale = new Vector3(3, 3, 3);
            sausage.Boosting = true;
            Matrix.Scaling(ref scale, out var mat);
            SpawnInstance(BoostVfx, sausage.Body, mat * Matrix.Translation(new(0, -2, 0)), duration * 2 + 5, duration);
            Script.Scheduler.Add(async () =>
            {
                await Script.Wait(3);
                sausage.Boosting = false;
            });
        }
        protected void SpawnInstance(Prefab source, Entity attachEntity, Matrix localMatrix, float timeout, float emitDuration = -1)
        {
            if (source == null)
                return;

            Func<Task> spawnTask = async () =>
            {
                // Clone
                var spawnedEntities = source.Instantiate();
                List<ParticleEmitter> emitters = [];
                foreach (var e in spawnedEntities)
                {
                    var ps = e.Get<ParticleSystemComponent>();
                    if (ps != null)
                    {
                        emitters.AddRange(ps.ParticleSystem.Emitters);
                    }
                }
                if (emitDuration != -1)
                {
                    Script.AddTask(async () =>
                    {
                        await Script.Wait(emitDuration);
                        foreach (var e in emitters)
                        {
                            e.CanEmitParticles = false;
                        }
                    });
                }
                // Add
                foreach (var prefabEntity in spawnedEntities)
                {
                    prefabEntity.Transform.UpdateLocalMatrix();
                    var entityMatrix = prefabEntity.Transform.LocalMatrix * localMatrix;
                    entityMatrix.Decompose(out prefabEntity.Transform.Scale, out prefabEntity.Transform.Rotation, out prefabEntity.Transform.Position);

                    if (attachEntity != null)
                    {
                        attachEntity.AddChild(prefabEntity);
                    }
                    else
                    {
                        SceneSystem.SceneInstance.RootScene.Entities.Add(prefabEntity);
                    }
                }

                await Script.Wait(timeout);

                // Remove
                foreach (var clonedEntity in spawnedEntities)
                {
                    if (attachEntity != null)
                    {
                        attachEntity.RemoveChild(clonedEntity);
                    }
                    else
                    {
                        SceneSystem.SceneInstance.RootScene.Entities.Remove(clonedEntity);
                    }
                }

                // Cleanup
                spawnedEntities.Clear();
            };

            Script.AddTask(spawnTask);
        }

    }
}
