using System;
using BepInEx;
using BepInEx.Configuration;
using IL;
using MonoMod.Cil;
using On;
using UnityEngine;


namespace pitrespawn
{
    [BepInPlugin("com.wonda.pitrespawn", "PitRespawn", "1.1.0")]
    public class PitRespawn : BaseUnityPlugin
    {
        // Our variables.
        // Storing a protected entity.
        private UpdatableAndDeletable respawnStorage = null;
        // Tracking the last room.
        private int lastRoom = -1;
        // Penalty scaling for repeated falls in the same room.
        private int failScale = 0;


        // Hooking into the ModsInit option.
        public void OnEnable() {
            On.RainWorld.OnModsInit += SetupHooks;
        }

        // Setting up our hooks.
        public void SetupHooks(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            MachineConnector.SetRegisteredOI("wonda_pitrespawn", new PitRespawnOptions());
            On.Creature.Die += Creature_Die;
            On.RainWorldGame.GameOver += RainWorldGame_GameOver;
            On.UpdatableAndDeletable.Destroy += UpdatableAndDeletable_Destroy;
            On.RoomCamera.ChangeRoom += RoomCamera_ChangeRoom;
        }

        // This is the easiest hook I can get. It'll allow the mod to stay updated on the last rooms whenever it changes for the camera.
        private void RoomCamera_ChangeRoom(On.RoomCamera.orig_ChangeRoom orig, RoomCamera self, Room newRoom, int cameraPosition)
        {
            // Updating our last room if we're currently in a room.
            if(self.room != null)
            {
                lastRoom = self.room.abstractRoom.index;
                failScale = 0;
            }
            orig(self, newRoom, cameraPosition);
        }

        // When it's possible to game over, we check the first available player we can and ensure that they live.
        private void RainWorldGame_GameOver(On.RainWorldGame.orig_GameOver orig, RainWorldGame self, Creature.Grasp dependentOnGrasp)
        {
            // Grabbing the first player we can get.
            Player fallenPlayer = self.FirstRealizedPlayer;

            // If the player is below the death plane and has food, we respawn them.
            if (IsBelowDeathPlane(fallenPlayer) && fallenPlayer.FoodInStomach >= 1)
            {
                // Move player and subtract food.
                MovePlayerToPreviousExit(fallenPlayer);
                fallenPlayer.SubtractFood(PitRespawnOptions.ScalingPenalty.Value ? failScale : PitRespawnOptions.FallPenalty.Value);
            }

            // If we already have the player in storage, skip trying to kill them.
            if (fallenPlayer == respawnStorage) return;
            orig(self, dependentOnGrasp);
        }

        // When any creature dies, we will check and see if we can plop them back on the surface.
        // Obviously, in the mod's current state, this behavior has been hard-coded to be disabled.
        private void Creature_Die(On.Creature.orig_Die orig, Creature self)
        {
            // If the 
            if (IsBelowDeathPlane(self))
            {
                MoveCreatureToPreviousNode(self);
            }

            if (self == respawnStorage) return;
            orig(self);
        }

        // The last bit, to catch when a creature we saved is slated for deletion.
        private void UpdatableAndDeletable_Destroy(On.UpdatableAndDeletable.orig_Destroy orig, UpdatableAndDeletable self)
        {
            if (self == respawnStorage)
            {
                respawnStorage = null;
                return;
            };
            orig(self);
        }

        // Operations.
        // Checking to see if the creature is below the death plane.
        private bool IsBelowDeathPlane(Creature self)
        {
            Debug.Log("Checking for below death plane");

            return (self.mainBodyChunk.pos.y < 0f &&
                (!self.room.water || self.room.waterInverted || self.room.defaultWaterLevel < -10) &&
                (!self.Template.canFly || self.Stunned || self.dead));
        }

        // Sending the player to the last exit they were at.
        private void MovePlayerToPreviousExit(Player self)
        {
            // Grabbing the default exit.
            int num = 0;
            // Setting our exit to the one we last left out of.
            num = self.room.abstractRoom.ExitIndex(lastRoom);
            // If the exit is invalid, then we'll pick a random one.
            if(num == -1) num = self.room.abstractRoom.ExitIndex(RXRandom.Int(self.room.abstractRoom.exits));
            // Setting the Realized position of the items.
            Vector2 shortcutPosition = new Vector2(self.room.LocalCoordinateOfNode(num).x * 20f, self.room.LocalCoordinateOfNode(num).y * 20f);

            // Reset the player's momemntum so they don't die from fall damage.
            for(var i = 0; i < self.bodyChunks.Length; i++)
            {
                self.bodyChunks[i].vel = Vector2.zero;
            }

            // Hard-setting the new player position.
            self.SuperHardSetPosition(shortcutPosition);

            // Incrementing the fail scale.
            failScale++;

            // Adding the player to the storage.
            respawnStorage = self;
        }

        // For future support of other creatures surviving falls.
        private void MoveCreatureToPreviousNode(Creature self)
        {
            
        }
    }
}
