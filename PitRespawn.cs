using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using IL;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using On;
using UnityEngine;

// TODO:
// Hook into RegionState.AdaptWorldToRegionState() and replace RandomNodeInRoom with our own LastRoom node function. (For key items~)
// Add items to the system?
// Iron out edge cases.
// Alter creature respawns to be more diagetic.

namespace pitrespawn
{
    [BepInPlugin("com.wonda.pitrespawn", "PitRespawn", "1.2.1")]
    public class PitRespawn : BaseUnityPlugin
    {
        // Metadata (Idea stolen from Schbaum)
        public static readonly string MOD_ID = "com.wonda.pitrespawn";
        public static readonly string author = "Wonda";
        public static readonly string version = "1.2.1";

        // Our variables.
        // A player storage variable to keep an eye on every slugcat.
        private class PlayerStorage
        {
            public PlayerStorage(Player _player)
            {
                player = _player;
            }

            // The player that's stored.
            public Player player;
            // The last room the player was in.
            public int lastRoom = -1;
            // The player's penalty for falling again.
            public int failScale = PitRespawnOptions.FallPenalty.Value;
            // The player's held items when they fall.
            public List<AbstractPhysicalObject> heldObjects = new List<AbstractPhysicalObject>();
        }
        // Storing our players to keep an eye on.
        private List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        // Storing an entity that we'll respawn.
        private List<UpdatableAndDeletable> currentlyRespawningEntities = new List<UpdatableAndDeletable>();
        // Storing the game for easy access. ;)
        private RainWorldGame currentGame;

        // Hooking into the ModsInit option.
        public void OnEnable() {
            On.RainWorld.OnModsInit += SetupHooks;
        }

        // Setting up our hooks.
        private void SetupHooks(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            MachineConnector.SetRegisteredOI("wonda_pitrespawn", new PitRespawnOptions());
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.Player.ctor += Player_ctor;
            On.AbstractCreature.Move += AbstractCreature_Move;
            On.Player.Die += Player_Die;
            On.Player.PermaDie += Player_PermaDie;
            On.Player.Destroy += Player_Destroy;
            On.Creature.Die += Creature_Die;
            On.UpdatableAndDeletable.Destroy += UpdatableAndDeletable_Destroy;
            //ReviseRegionState();
        }

        // Getting our game hook to avoid shenanigans with always keeping an eye on players.
        private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);
            currentGame = self;
            playerStorage.Clear();
        }

        // Replace RegionState.AdaptWorldToRegionState() to use our lastRoom function.
        // Also depreciated.
        //private void ReviseRegionState()
        //{
        //    IL.RegionState.AdaptRegionStateToWorld += (il) =>
        //    {
        //        ILCursor c = new ILCursor(il);
        //        c.GotoNext(
        //            x => x.MatchLdloc(9),
        //            x => x.MatchLdloc(10),
        //            x => x.MatchCallvirt<AbstractRoom>("RandomNodeInRoom")
        //            );
        //        c.Index += 2;
        //        c.Remove();
        //        c.EmitDelegate<Func<WorldCoordinate>>(GetPlayersLastPipe);

        //        Debug.Log("OO EE OO " + il.ToString());
        //    };
        //}
        // Catching the player creation to drop them into our player storage.
        private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            Logger.LogDebug("Player " + self.playerState.playerNumber + " Initalized.");
            playerStorage.Add(new PlayerStorage(self));
        }

        // Catching whenever an entity moves.
        private void AbstractCreature_Move(On.AbstractCreature.orig_Move orig, AbstractCreature self, WorldCoordinate newCoord)
        {
            // Checking to see if the entity was realized.
            if (self.realizedCreature != null && self.realizedCreature.lastCoord != null && self.realizedCreature.lastCoord != newCoord && currentGame.world.GetAbstractRoom(self.realizedCreature.lastCoord) != null && currentGame.world.GetAbstractRoom(newCoord) != null)
            {
                Logger.LogDebug(self.creatureTemplate.name + " of " + self.realizedCreature.GetType() + " moved from " + currentGame.world.GetAbstractRoom(self.realizedCreature.lastCoord).name + " to " + currentGame.world.GetAbstractRoom(newCoord).name);

                // Checking to see if the entity was a player.
                if (self.realizedCreature is Player)
                { 

                    // Lil' helper variable.
                    Player target = (Player)self.realizedCreature;
                    // Checking to see if the player has entered the same room
                    if (playerStorage.Find(x => x.player == target).lastRoom == currentGame.world.GetAbstractRoom(self.realizedCreature.lastCoord).index) return;

                    // Assigning the relevant player's last room to their current room.
                    playerStorage.Find(x => x.player == target).lastRoom = currentGame.world.GetAbstractRoom(self.realizedCreature.lastCoord).index;
                    // Debugging.
                    Logger.LogDebug("Player " + target.playerState.playerNumber + " has last room of " + currentGame.world.GetAbstractRoom(playerStorage.Find(x => x.player == target).lastRoom).name + " and is moving to " + currentGame.world.GetAbstractRoom(newCoord).name + ".");
                }
            }

            orig(self, newCoord);
        }

        // Catching a player's death and respawning them if it is just a fall.
        private void Player_Die(On.Player.orig_Die orig, Player self)
        {
            Logger.LogDebug("Player " + self.playerState.playerNumber + " has died! Why?");

            int subtractedFoodAmount = PitRespawnOptions.ScalingPenalty.Value ? playerStorage.Find(x => x.player == self).failScale : PitRespawnOptions.FallPenalty.Value;

            if (PitRespawnOptions.PlayerRespawn.Value && !self.dead && IsBelowDeathPlane(self) && self.FoodInStomach >= 1 && (PitRespawnOptions.SecondChance.Value || subtractedFoodAmount <= self.FoodInStomach))
            {
                Logger.LogDebug("Player died from falling and has food to spare. Attempting respawn.");
                MoveCreatureToPreviousNode(self);
                self.SubtractFood(Math.Min(subtractedFoodAmount, self.FoodInStomach));
                return;
            }
            if (currentlyRespawningEntities.Contains(self)) return;

            Logger.LogDebug("Killing the player anyways.");
            orig(self);
        }

        // Also catching permadies.
        private void Player_PermaDie(On.Player.orig_PermaDie orig, Player self)
        {
            if (PitRespawnOptions.PlayerRespawn.Value && currentlyRespawningEntities.Contains(self))
            {
                //currentlyRespawningEntity = null;
                return;
            };
            orig(self);
        }

        // Just a copy to catch any issue with multiplayer.
        private void Player_Destroy(On.Player.orig_Destroy orig, Player self)
        {
            if (PitRespawnOptions.PlayerRespawn.Value && currentlyRespawningEntities.Contains(self))
            {
                currentlyRespawningEntities.Remove(self);
                return;
            };
            orig(self);
        }

        // When any creature dies, we will check and see if we can plop them back on the surface.
        // Obviously, in the mod's current state, this behavior has been hard-coded to be disabled.
        private void Creature_Die(On.Creature.orig_Die orig, Creature self)
        {
            // If the 
            if (PitRespawnOptions.CreatureRespawn.Value && !self.dead && IsBelowDeathPlane(self))
            {
                MoveCreatureToPreviousNode(self);
            }

            if (currentlyRespawningEntities.Contains(self)) return;
            orig(self);
        }

        // The last bit, to catch when a creature we saved is slated for deletion.
        private void UpdatableAndDeletable_Destroy(On.UpdatableAndDeletable.orig_Destroy orig, UpdatableAndDeletable self)
        {
            if (currentlyRespawningEntities.Contains(self))
            {
                currentlyRespawningEntities.Remove(self);
                return;
            };
            orig(self);
        }

        // Operations.
        // Checking to see if the creature is below the death plane.
        private bool IsBelowDeathPlane(Creature self)
        {
            Logger.LogDebug("Checking for below death plane");

            return (self.mainBodyChunk.pos.y < 0f &&
                (!self.room.water || self.room.waterInverted || self.room.defaultWaterLevel < -10) &&
                (!self.Template.canFly || self.Stunned || self.dead));
        }

        // This function grabs the given player's last pipe.
        private WorldCoordinate GetPlayersLastPipe(Player player)
        {
            // Grabbing the player's previous room node.
            int num = player.room.abstractRoom.ExitIndex(playerStorage.Find(x => x.player == player).lastRoom);

            Logger.LogDebug("Player's last pipe had an index of " + num);

            // If the exit is invalid, we'll just grab a random node in the room.
            if (num == -1) return player.room.LocalCoordinateOfNode(player.room.abstractRoom.ExitIndex(player.room.abstractRoom.connections[UnityEngine.Random.Range(0, player.room.abstractRoom.connections.Length)]));

            // If the exit is valid, then we'll return the exit we grabbed.
            return player.room.LocalCoordinateOfNode(num);
        }

        // For future support of other creatures surviving falls.
        private void MoveCreatureToPreviousNode(Creature self)
        {
            // Prepping our coordinates.
            WorldCoordinate shortcutCordinate;
            if (self is Player)
            {
                // If the creature is a player, we send them to their last pipe.
                shortcutCordinate = GetPlayersLastPipe((Player)self);
            } else
            {
                // Otherwise we just send them to a random node.
                shortcutCordinate = self.room.LocalCoordinateOfNode(self.room.abstractRoom.ExitIndex(self.room.abstractRoom.connections[UnityEngine.Random.Range(0, self.room.abstractRoom.connections.Length)]));
            }

            Logger.LogDebug(self.Template.name + "'s last pipe was at (" + shortcutCordinate.x + ", " + shortcutCordinate.y + "). In " + self.room.abstractRoom.name + ".");

            // Storing every item that the creature had.
            List<PhysicalObject> graspStorage = new List<PhysicalObject>();

            // Iterating over every grasped item and adding them to our item storage.
            if(self.grasps != null && self.grasps.Length > 0)
            {
                foreach (Creature.Grasp grasp in self.grasps)
                {
                    if (grasp != null && grasp.grabbed != null)
                    {
                        graspStorage.Add(grasp.grabbed);
                        //currentlyRespawningEntities.Add(grasp.grabbed);
                    }
                }
            }

            // Causing the creature to lose all grasps.
            self.LoseAllGrasps();

            // Spitting the player out of the shortcut.
            self.SpitOutOfShortCut(new RWCustom.IntVector2(shortcutCordinate.x, shortcutCordinate.y), self.room, false);

            // Iterating over every stored item and spawning them at the pipe.
            for(var i = 0; i < graspStorage.Count; i++)
            {
                foreach(BodyChunk chunk in graspStorage[i].bodyChunks)
                {
                    chunk.HardSetPosition(new Vector2(shortcutCordinate.x * 20f, shortcutCordinate.y * 20f) + RWCustom.Custom.RNV());
                    chunk.vel *= 0f;
                }
            }

            // If the creature is a player, give them their items back.
            if (!PitRespawnOptions.DropItemsOnRespawn.Value && self is Player)
            {
                foreach (PhysicalObject item in graspStorage)
                {
                    if(item != null && (self as Player).FreeHand() != -1)
                        (self as Player).SlugcatGrab(item, (self as Player).FreeHand());
                }
            }

            // Adding the creature to the storage.
            currentlyRespawningEntities.Add(self);
        }
    }
}
