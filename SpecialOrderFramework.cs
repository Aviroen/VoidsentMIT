using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.GameData;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;
using StardewValley.Extensions;
using StardewValley.Characters;
//code from Ridgeside Village https://github.com/Rafseazz/Ridgeside-Village-Mod

namespace Voidsent.Framework
{
    internal class VSSpecialOrderBoard : SpecialOrdersBoard
    {
        private static IMonitor Monitor { get; set; } = null!;
        private static IModHelper Helper { get; set; } = null!;
        private int timestampOpened;
        static int safetyTimer = 500;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            Monitor = monitor;
            Helper = helper;
        }

        internal VSSpecialOrderBoard(string board_type = "Aviroen.VoidsentCP") : base(board_type)
        {
            UpdateAvailableVSSpecialOrders(board_type, forceRefresh: false);
            Texture2D texture;
            if (board_type.Equals("Aviroen.VoidsentCP"))
            {
                texture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\SpecialOrdersBoard");
                //change out the texture to "LooseSprites\\Aviroen.VoidsentCP_Board" ya dingus
            }
            else
            {
                texture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\SpecialOrdersBoard");
            }
            Helper.Reflection.GetField<Texture2D>(this, "billboardTexture").SetValue(texture);
        }
        public static bool OpenVSBoard(GameLocation location, string[] args, Farmer who, Point tile)
        {
            string subAction = ArgUtility.Get(args, 1);
            if (subAction == null)
            {
                return false;
            }
            else
            {
                Game1.activeClickableMenu = new VSSpecialOrderBoard();
                return true;
            }
        }
        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (timestampOpened + safetyTimer < Game1.currentGameTime.TotalGameTime.TotalMilliseconds)
            {
                base.receiveRightClick(x, y, playSound);
            }
            return;
        }
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (timestampOpened + safetyTimer > Game1.currentGameTime.TotalGameTime.TotalMilliseconds)
            {
                return;
            }
            base.receiveLeftClick(x, y, playSound);
        }
        public override void draw(SpriteBatch b)
        {
            base.draw(b);
            if (leftOrder is null)
            {
                b.DrawString(Game1.dialogueFont, Helper.Translation.Get("Aviroen.Voidsent_NoNewOrder"), new Vector2(xPositionOnScreen + 125, yPositionOnScreen + 375), Game1.textColor);
            }
            if (rightOrder is null)
            {
                int indent = (boardType == "Aviroen.VoidsentCP") ? 800 : 775;
                b.DrawString(Game1.dialogueFont, Helper.Translation.Get("Aviroen.Voidsent_NoNewOrder"), new Vector2(xPositionOnScreen + indent, yPositionOnScreen + 375), Game1.textColor);
            }
        }
        public static void UpdateAvailableVSSpecialOrders(string orderType, bool forceRefresh)
        {
            foreach (SpecialOrder order in Game1.player.team.availableSpecialOrders)
            {
                if (orderType == "Aviroen.VoidsentCP")
                {
                    if ((order.questDuration.Value == QuestDuration.TwoDays || order.questDuration.Value == QuestDuration.ThreeDays) && !Game1.player.team.acceptedSpecialOrderTypes.Contains(order.orderType.Value))
                    {
                        order.SetDuration(order.questDuration.Value);
                    }
                }
            }
            if (!forceRefresh)
            {
                foreach (SpecialOrder availableSpecialOrder in Game1.player.team.availableSpecialOrders)
                {
                    if (orderType == "Aviroen.VoidsentCP")
                    {
                        if (availableSpecialOrder.orderType.Value == orderType)
                        {
                            return;
                        }
                    }
                }
            }
            SpecialOrder.RemoveAllSpecialOrders(orderType);
            List<string> keyQueue = new List<string>();
            foreach (KeyValuePair<string, SpecialOrderData> pair in DataLoader.SpecialOrders(Game1.content))
            {
                if (orderType == "Aviroen.VoidsentCP")
                {
                    if (pair.Value.OrderType == orderType && SpecialOrder.CanStartOrderNow(pair.Key, pair.Value))
                    {
                        keyQueue.Add(pair.Key);
                    }
                }
            }
            List<string> keysIncludingCompleted = new List<string>(keyQueue);
            if (orderType == "Aviroen.VoidsentCP")
            {
                keyQueue.RemoveAll((string id) => Game1.player.team.completedSpecialOrders.Contains(id));
            }
            Random r = Utility.CreateRandom(Game1.uniqueIDForThisGame, (double)Game1.stats.DaysPlayed * 1.3);
            for (int i = 0; i < 2; i++)
            {
                if (keyQueue.Count == 0)
                {
                    if (keysIncludingCompleted.Count == 0)
                    {
                        break;
                    }
                    keyQueue = new List<string>(keysIncludingCompleted);
                }
                string key = r.ChooseFrom(keyQueue);
                Game1.player.team.availableSpecialOrders.Add(SpecialOrder.GetSpecialOrder(key, r.Next()));
                keyQueue.Remove(key);
                keysIncludingCompleted.Remove(key);
            }
        }
    }
}
