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
    [BepInPlugin("com.wonda.pitrespawn", "PitRespawn", "1.2.0")]
    public class PitRespawn : BaseUnityPlugin
    {
        // Metadata (Idea stolen from Schbaum)
        public static readonly string MOD_ID = "com.wonda.pitrespawn";
        public static readonly string author = "Wonda";
        public static readonly string version = "1.2.0";

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
            public int failScale = 0;
        }
        // Storing our players to keep an eye on.
        private List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        // Storing an entity that we'll respawn.
        private UpdatableAndDeletable currentlyRespawningEntity = null;
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
            Logger.LogInfo("Player " + self.playerState.playerNumber + " Initalized.");
            playerStorage.Add(new PlayerStorage(self));
        }

        // Catching whenever an entity moves.
        private void AbstractCreature_Move(On.AbstractCreature.orig_Move orig, AbstractCreature self, WorldCoordinate newCoord)
        {
            // Checking to see if the entity was realized.
            if (self.realizedCreature != null && self.realizedCreature.lastCoord != newCoord)
            {
                Logger.LogInfo(self.creatureTemplate.name + " of " + self.realizedCreature.GetType() + " moved to " + currentGame.world.GetAbstractRoom(newCoord).index);

                // Checking to see if the entity was a player.
                if (self.realizedCreature is Player)
                {
                    // Lil' helper variable.
                    Player target = (Player)self.realizedCreature;
                    // Checking to see if the player has entered the same room
                    if (playerStorage.Find(x => x.player == target).lastRoom == currentGame.world.GetAbstractRoom(self.pos).index) return;

                    // Assigning the relevant player's last room to their current room.
                    playerStorage.Find(x => x.player == target).lastRoom = currentGame.world.GetAbstractRoom(self.pos).index;
                    // Debugging.
                    Logger.LogInfo("Player " + target.playerState.playerNumber + " has last room of " + playerStorage.Find(x => x.player == target).lastRoom + " and is moving to " + currentGame.world.GetAbstractRoom(newCoord).index + ".");
                }
            }
            orig(self, newCoord);
        }

        // Catching a player's death and respawning them if it is just a fall.
        private void Player_Die(On.Player.orig_Die orig, Player self)
        {
            Logger.LogInfo("Player " + self.playerState.playerNumber + " has died! Why?");

            int subtractedFoodAmount = PitRespawnOptions.ScalingPenalty.Value ? playerStorage.Find(x => x.player == self).failScale : PitRespawnOptions.FallPenalty.Value;

            if (PitRespawnOptions.PlayerRespawn.Value && !self.dead && IsBelowDeathPlane(self) && self.FoodInStomach >= 1 && (PitRespawnOptions.SecondChance.Value || subtractedFoodAmount <= self.FoodInStomach))
            {
                Logger.LogInfo("Player died from falling and has food to spare. Attempting respawn.");
                MovePlayerToPreviousExit(self);
                self.SubtractFood(Math.Min(subtractedFoodAmount, self.FoodInStomach));
                return;
            }
            if (self == currentlyRespawningEntity) return;
            orig(self);
        }

        // Also catching permadies.
        private void Player_PermaDie(On.Player.orig_PermaDie orig, Player self)
        {
            if (PitRespawnOptions.PlayerRespawn.Value && self == currentlyRespawningEntity)
            {
                //currentlyRespawningEntity = null;
                return;
            };
            orig(self);
        }

        // Just a copy to catch any issue with multiplayer.
        private void Player_Destroy(On.Player.orig_Destroy orig, Player self)
        {
            if (PitRespawnOptions.PlayerRespawn.Value && self == currentlyRespawningEntity)
            {
                currentlyRespawningEntity = null;
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

            if (self == currentlyRespawningEntity) return;
            orig(self);
        }

        // The last bit, to catch when a creature we saved is slated for deletion.
        private void UpdatableAndDeletable_Destroy(On.UpdatableAndDeletable.orig_Destroy orig, UpdatableAndDeletable self)
        {
            if (self == currentlyRespawningEntity)
            {
                currentlyRespawningEntity = null;
                return;
            };
            orig(self);
        }

        // Operations.
        // Checking to see if the creature is below the death plane.
        private bool IsBelowDeathPlane(Creature self)
        {
            Logger.LogInfo("Checking for below death plane");

            return (self.mainBodyChunk.pos.y < 0f &&
                (!self.room.water || self.room.waterInverted || self.room.defaultWaterLevel < -10) &&
                (!self.Template.canFly || self.Stunned || self.dead));
        }

        // This function grabs the given player's last pipe.
        private WorldCoordinate GetPlayersLastPipe(Player player)
        {
            // Grabbing the player's previous room node.
            int num = player.room.abstractRoom.ExitIndex(playerStorage.Find(x => x.player == player).lastRoom);

            Logger.LogInfo("Player's last pipe had an index of " + num);

            // If the exit is invalid, we'll just grab a random node in the room.
            if (num == -1) return player.room.abstractRoom.RandomNodeInRoom();

            // If the exit is valid, then we'll return the exit we grabbed.
            return player.room.LocalCoordinateOfNode(num);
        }

        // Sending the player to the last exit they were at.
        private void MovePlayerToPreviousExit(Player self)
        {
            // Getting the cordinates of the player's last pipe.
            WorldCoordinate shortcutCordinate = GetPlayersLastPipe(self);

            // Setting the Realized position of the player.
            Vector2 shortcutPosition = new Vector2(shortcutCordinate.x * 20f, shortcutCordinate.y * 20f);

            Logger.LogInfo("Player's last pipe was at (" + shortcutPosition.x + "f, " + shortcutPosition.y + "f).");

            // Reset the player's momemntum so they don't die from fall damage.
            for (var i = 0; i < self.bodyChunks.Length; i++)
            {
                self.bodyChunks[i].vel = Vector2.zero;
            }

            // Hard-setting the new player position.
            self.SuperHardSetPosition(shortcutPosition);

            // Incrementing the fail scale.
            playerStorage.Find(x => x.player == self).failScale++;

            // Adding the player to the storage.
            currentlyRespawningEntity = self;
        }

        // For future support of other creatures surviving falls.
        private void MoveCreatureToPreviousNode(Creature self)
        {
            // Getting the cordinates of a random pipe.
            WorldCoordinate shortcutCordinate = self.room.LocalCoordinateOfNode(self.room.abstractRoom.ExitIndex(self.room.abstractRoom.connections[UnityEngine.Random.Range(0, self.room.abstractRoom.connections.Length)]));

            // Setting the Realized position of the creature.
            Vector2 shortcutPosition = new Vector2(shortcutCordinate.x * 20f, shortcutCordinate.y * 20f);

            Logger.LogInfo("Creature's last pipe was at (" + shortcutPosition.x + "f, " + shortcutPosition.y + "f). In " + self.room.abstractRoom.name + ".");

            // Moving the creature and setting their velocity.
            for (var i = 0; i < self.bodyChunks.Length; i++)
            {
                self.bodyChunks[i].vel = Vector2.zero;
                self.bodyChunks[i].pos = shortcutPosition;
            }

            // Adding the player to the storage.
            currentlyRespawningEntity = self;
        }
    }
}
