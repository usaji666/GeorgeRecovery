using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace GeorgeRecovery;

public sealed class ModEntry : Mod
{
    private const string SaveKey = "quest-state";
    private const string GeorgeHouse = "JoshHouse";
    private const string HarveyRoom = "HarveyRoom";
    private const string HarveyRefrigeratorAction = "HarveyRoom.12";
    private const string GeorgeDialogueAsset = "Characters/Dialogue/George";
    private const string GeorgeScheduleAsset = "Characters/schedules/George";
    private const string StandingGeorgeAsset = "Characters/George_Standing";
    private const string RecoveryHomeSchedule = "vicky_recovery_home";
    private const string RecoverySpringSchedule = "vicky_recovery_spring";
    private const string RecoverySummerSchedule = "vicky_recovery_summer";
    private const string RecoveryCommunityCenterSchedule = "vicky_recovery_community_center";
    private const string RecoveryClinicSchedule = "vicky_recovery_clinic";
    private const string RecoveryBeachSchedule = "vicky_recovery_beach";
    private const string AcceptedBranchId = "vicky.GeorgeRecovery.AcceptedBranch";
    private const string AcceptedFlag = "vicky.GeorgeRecovery.Accepted";
    private const string DeclinedFlag = "vicky.GeorgeRecovery.Declined";
    private const string CompletedFlag = "vicky.GeorgeRecovery.Completed";

    private readonly Dictionary<string, MaterialRequirement> requirements = new()
    {
        ["(O)446"] = new("兔子的脚", 1, data => data.RabbitFootSubmitted, (data, value) => data.RabbitFootSubmitted = value)
    };

    private SaveData data = new();
    private Chest? taskChest;
    private Point? refrigeratorTile;
    private bool taskChestMenuOpen;
    private bool openingSceneStarted;
    private bool finaleSceneStarted;
    private bool lastRecoveryComplete;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Display.MenuChanged += this.OnMenuChanged;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        helper.Events.Content.AssetRequested += this.OnAssetRequested;

        helper.ConsoleCommands.Add(
            "george_recovery_status",
            "显示治疗乔治腿伤任务的当前状态。",
            this.OnStatusCommand
        );
        helper.ConsoleCommands.Add(
            "george_recovery_setstage",
            "测试用：设置任务阶段。可用值：notstarted、collecting、materials、declined、completed。",
            this.OnSetStageCommand
        );
    }

    private void UseStardewCursor()
    {
        // Keep Stardew Valley's brown software cursor, while hiding the macOS arrow.
        Game1.options.hardwareCursor = false;
        Game1.game1.IsMouseVisible = false;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(StandingGeorgeAsset))
        {
            e.LoadFromModFile<Texture2D>("assets/George_Standing.png", AssetLoadPriority.Exclusive);
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo("Data/Events/JoshHouse"))
        {
            e.Edit(asset =>
            {
                IDictionary<string, string> events = asset.AsDictionary<string, string>().Data;
                events[AcceptedBranchId] =
                    "emote Harvey 20/"
                    + "speak Harvey \"我就知道！@。$h\"/"
                    + "speak Harvey \"请将一个兔子的脚放到我的房间楼上的冰箱上。\"/"
                    + $"mailReceived {AcceptedFlag}/end";
            });
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(GeorgeScheduleAsset))
        {
            e.Edit(asset =>
            {
                IDictionary<string, string> schedules = asset.AsDictionary<string, string>().Data;
                schedules[RecoveryHomeSchedule] =
                    "630 JoshHouse 16 22 0/1210 JoshHouse 5 21 3/1330 JoshHouse 5 19 2/1500 JoshHouse 17 22 3/1630 JoshHouse 17 17 0/1700 JoshHouse 16 22 0/2000 JoshHouse 3 5 0 george_sleep";
                schedules[RecoverySpringSchedule] =
                    "630 JoshHouse 16 22 0/1210 JoshHouse 5 21 3/1300 Town 38 78 3/1600 JoshHouse 17 17 0/1700 JoshHouse 16 22 0/2000 JoshHouse 3 5 0 george_sleep";
                schedules[RecoverySummerSchedule] =
                    "630 JoshHouse 16 22 0/1210 JoshHouse 5 21 3/1300 Town 22 62 3/1600 JoshHouse 17 17 0/1700 JoshHouse 16 22 0/2000 JoshHouse 3 5 0 george_sleep";
                schedules[RecoveryCommunityCenterSchedule] =
                    "630 JoshHouse 16 22 0/1210 CommunityCenter 20 25 3/1600 JoshHouse 17 17 0/1700 JoshHouse 16 22 0/2000 JoshHouse 3 5 0 george_sleep";
                schedules[RecoveryClinicSchedule] =
                    "630 JoshHouse 16 22 0/1030 Hospital 12 15 0/1330 Hospital 6 6 1/1600 JoshHouse 17 17 0/1700 JoshHouse 16 22 0/2000 JoshHouse 3 5 0 george_sleep";
                schedules[RecoveryBeachSchedule] =
                    "630 JoshHouse 16 22 0/1210 JoshHouse 5 21 3/1620 Beach 10 39 2/1700 Beach 10 39 2/2340 JoshHouse 3 5 0 george_sleep";
            });
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(GeorgeDialogueAsset)
            && Context.IsWorldReady
            && IsRecoveryComplete())
        {
            e.Edit(asset =>
            {
                IDictionary<string, string> dialogue = asset.AsDictionary<string, string>().Data;
                dialogue["Wed8"] = "哈维说我的康复进展很不错。#$e#我已经能慢慢走上几步了，下次要让亚历克斯看看。$h";
                dialogue["Tue10"] = "我每天都会按哈维教的方法练习双腿。#$e#慢慢来，总会一天比一天更好。$h";
            });
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.UseStardewCursor();
        this.data = this.Helper.Data.ReadSaveData<SaveData>(SaveKey) ?? new SaveData();
        this.openingSceneStarted = false;
        this.finaleSceneStarted = false;
        this.taskChest = new Chest(true);
        this.refrigeratorTile = null;
        this.taskChestMenuOpen = false;
        this.SyncStageFromMailFlags();
        this.lastRecoveryComplete = IsRecoveryComplete();
        this.Helper.GameContent.InvalidateCache(GeorgeDialogueAsset);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.SyncStageFromMailFlags();
        this.lastRecoveryComplete = IsRecoveryComplete();
        this.Helper.GameContent.InvalidateCache(GeorgeDialogueAsset);

        if (this.lastRecoveryComplete)
            this.ApplyRecoveryScheduleForToday();

        this.UpdateGeorgeAppearance();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.Helper.Data.WriteSaveData(SaveKey, this.data);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsMultipleOf(15))
            return;

        this.UseStardewCursor();
        this.SyncStageFromMailFlags();

        bool recoveryComplete = IsRecoveryComplete();
        if (recoveryComplete != this.lastRecoveryComplete)
        {
            this.lastRecoveryComplete = recoveryComplete;
            this.Helper.GameContent.InvalidateCache(GeorgeDialogueAsset);

            if (recoveryComplete)
                this.ApplyRecoveryScheduleForToday();
            else
                this.RestoreGeorgeSchedule();
        }

        this.UpdateGeorgeAppearance();
    }

    private static bool IsRecoveryComplete()
    {
        return Context.IsWorldReady && Game1.player.mailReceived.Contains(CompletedFlag);
    }

    private void UpdateGeorgeAppearance()
    {
        NPC? george = Game1.getCharacterFromName("George");
        if (george is null)
            return;

        bool shouldStand = IsRecoveryComplete()
            && Game1.timeOfDay >= 1200
            && Game1.timeOfDay < 1700
            && !Game1.eventUp;
        bool isStanding = string.Equals(george.Sprite.loadedTexture, StandingGeorgeAsset, StringComparison.OrdinalIgnoreCase);

        if (shouldStand && !isStanding)
        {
            george.Sprite.LoadTexture(StandingGeorgeAsset, true);
            george.Sprite.CurrentFrame = george.FacingDirection switch
            {
                2 => 0,
                1 => 4,
                0 => 8,
                3 => 12,
                _ => 0
            };
            george.Sprite.UpdateSourceRect();
        }
        else if (!shouldStand && isStanding)
        {
            george.reloadSprite(true);
        }
    }

    private void ApplyRecoveryScheduleForToday()
    {
        if (!Context.IsMainPlayer || Game1.isFestival())
            return;

        NPC? george = Game1.getCharacterFromName("George");
        if (george is null)
            return;

        string scheduleKey = this.GetRecoveryScheduleKey();
        if (george.TryLoadSchedule(scheduleKey))
            this.Monitor.Log($"已为乔治载入康复日程：{scheduleKey}", LogLevel.Debug);
        else
            this.Monitor.Log($"无法载入乔治的康复日程：{scheduleKey}", LogLevel.Warn);
    }

    private string GetRecoveryScheduleKey()
    {
        int day = Game1.Date.DayOfMonth;
        string season = Game1.currentSeason;

        if (day is 2 or 23)
            return RecoveryClinicSchedule;
        if (season == "winter" && day == 17)
            return RecoveryBeachSchedule;
        if (Game1.isRaining || Game1.isGreenRain)
            return RecoveryHomeSchedule;
        if (Game1.Date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday or DayOfWeek.Saturday)
            return RecoveryCommunityCenterSchedule;
        if (season == "spring")
            return RecoverySpringSchedule;
        if (season == "summer")
            return RecoverySummerSchedule;

        return RecoveryHomeSchedule;
    }

    private void RestoreGeorgeSchedule()
    {
        NPC? george = Game1.getCharacterFromName("George");
        if (george is null)
            return;

        george.TryLoadSchedule();
        if (string.Equals(george.Sprite.loadedTexture, StandingGeorgeAsset, StringComparison.OrdinalIgnoreCase))
            george.reloadSprite(true);
    }

    private void SyncStageFromMailFlags()
    {
        if (Game1.player.mailReceived.Contains(CompletedFlag))
            this.data.Stage = QuestStage.Completed;
        else if (Game1.player.mailReceived.Contains(DeclinedFlag))
            this.data.Stage = QuestStage.Declined;
        else if (Game1.player.mailReceived.Contains(AcceptedFlag))
            this.data.Stage = this.data.HasAllMaterials ? QuestStage.MaterialsComplete : QuestStage.Collecting;

        if (this.data.HasAllMaterials && this.data.Stage == QuestStage.Collecting)
            this.data.Stage = QuestStage.MaterialsComplete;
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady || !e.IsLocalPlayer)
            return;

        this.refrigeratorTile = e.NewLocation.NameOrUniqueName == HarveyRoom
            ? this.FindRefrigeratorTile(e.NewLocation)
            : null;

        if (e.NewLocation.NameOrUniqueName != GeorgeHouse || Game1.eventUp)
            return;

        if (this.data.Stage == QuestStage.NotStarted && this.CanStartQuest() && !this.openingSceneStarted)
        {
            this.openingSceneStarted = true;
            this.StartOpeningScene(e.NewLocation);
        }
        else if (this.data.Stage == QuestStage.MaterialsComplete && CanShowFinale() && !this.finaleSceneStarted)
        {
            this.finaleSceneStarted = true;
            this.StartFinaleScene(e.NewLocation);
        }
    }

    private bool CanStartQuest()
    {
        return Game1.isRaining
            && Game1.timeOfDay >= 900
            && Game1.timeOfDay <= 1800
            && Game1.player.getFriendshipHeartLevelForNPC("George") >= 8
            && Game1.player.getFriendshipHeartLevelForNPC("Harvey") >= 8
            && !Game1.player.mailReceived.Contains(AcceptedFlag)
            && !Game1.player.mailReceived.Contains(DeclinedFlag);
    }

    private static bool CanShowFinale()
    {
        return Game1.timeOfDay >= 900
            && Game1.timeOfDay <= 1800
            && !Game1.isFestival();
    }

    private void StartOpeningScene(GameLocation location)
    {
        // A real event keeps the house visible, moves the actors on the map, and uses NPC portraits.
        string script =
            "none/-1000 -1000/farmer 14 21 1 George 17 21 3/"
            + "skippable/viewport 16 21 true/pause 500/"
            + "move farmer 2 0 1/faceDirection George 3/pause 300/"
            + "speak George \"每次下雨天我的腿都疼得厉害。$s\"/"
            + "speak George \"哈维每周都来帮我做康复训练，希望有朝一日我能站起来。\"/"
            + "speak George \"艾芙琳承担了太多，我有时痛恨自己站不起来。$s\"/"
            + "pause 1000/"
            + "addTemporaryActor Harvey 16 32 13 21 1 true Character Harvey/playSound doorClose/"
            + "ignoreCollisions Harvey/"
            + "advancedMove Harvey false 0 1 3 0/proceedPosition Harvey/stopAdvancedMoves/"
            + "faceDirection Harvey 0/faceDirection farmer 1/faceDirection George 3/pause 300/"
            + "speak Harvey \"乔治的腿伤很严重，不过不是一点办法没有。\"/"
            + "speak Harvey \"@，你愿意帮我吗？\"/"
            + "question fork0 \"你是否愿意接受哈维的请求？#是。#否。\"/"
            + $"fork {AcceptedBranchId}/"
            + "friendship Harvey -50/friendship George -50/emote Harvey 12/"
            + "speak Harvey \"好吧，我自己帮乔治也不是不行。$s\"/"
            + $"mailReceived {DeclinedFlag}/end";

        location.startEvent(new Event(script, Game1.player));
    }

    private void StartFinaleScene(GameLocation location)
    {
        string script =
            "none/-1000 -1000/farmer 14 21 1 George 17 21 3/"
            + "skippable/viewport 16 21 true/pause 500/"
            + "move farmer 2 0 1/faceDirection George 3/pause 300/"
            + "emote George 20/"
            + "speak George \"嘿，@，哈维是个好医生，他成功治疗了我的腿伤，虽然走得还不利索。$h\"/"
            + "speak George \"也多亏了你，我的腿才能好得这么快。$h\"/"
            + "speak George \"我会继续做康复训练。等准备好以后，我想亲自走到镇上看看。$h\"/"
            + $"mailReceived {CompletedFlag}/end";

        location.startEvent(new Event(script, Game1.player));
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady
            || !Context.IsPlayerFree
            || this.data.Stage != QuestStage.Collecting
            || Game1.currentLocation.NameOrUniqueName != HarveyRoom
            || !e.Button.IsActionButton())
        {
            return;
        }

        Point target = e.Cursor.GrabTile.ToPoint();
        if (!this.IsRefrigeratorInteraction(Game1.currentLocation, target))
            return;

        this.Helper.Input.Suppress(e.Button);
        this.OpenTaskChest();
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady
            || this.data.Stage != QuestStage.Collecting
            || Game1.currentLocation.NameOrUniqueName != HarveyRoom
            || this.refrigeratorTile is not Point fridge)
        {
            return;
        }

        float bounce = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250d) * 4f;
        Vector2 worldPosition = new(fridge.X * Game1.tileSize + 26f, fridge.Y * Game1.tileSize - 42f + bounce);
        Vector2 screenPosition = Game1.GlobalToLocal(Game1.viewport, worldPosition);

        e.SpriteBatch.Draw(
            Game1.mouseCursors,
            screenPosition,
            new Rectangle(395, 497, 3, 8),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            1f
        );
    }

    private void OpenTaskChest()
    {
        this.taskChest ??= new Chest(true);
        this.taskChest.Items.Clear();
        Game1.activeClickableMenu = new ItemGrabMenu(
            inventory: this.taskChest.Items,
            reverseGrab: false,
            showReceivingMenu: true,
            highlightFunction: IsRabbitFoot,
            behaviorOnItemSelectFunction: this.taskChest.grabItemFromInventory,
            message: "哈维的治疗材料箱",
            behaviorOnItemGrab: this.taskChest.grabItemFromChest,
            snapToBottom: false,
            canBeExitedWithKey: true,
            playRightClickSound: true,
            allowRightClick: true,
            showOrganizeButton: true,
            source: ItemGrabMenu.source_chest,
            sourceItem: this.taskChest,
            whichSpecialButton: -1,
            context: this.taskChest
        );
        this.taskChestMenuOpen = true;
        Game1.playSound("openChest");
    }

    private static bool IsRabbitFoot(Item item)
    {
        // QualifiedItemId doesn't include quality, so normal, silver, gold, and iridium all match.
        return item.QualifiedItemId == "(O)446";
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!this.taskChestMenuOpen || e.NewMenu is ItemGrabMenu)
            return;

        this.taskChestMenuOpen = false;
        this.ProcessTaskChest();
    }

    private void ProcessTaskChest()
    {
        List<string> submitted = new();

        if (this.taskChest is null)
            return;

        List<Item> deposited = this.taskChest.Items.Where(item => item is not null).ToList();
        this.taskChest.Items.Clear();

        foreach ((string qualifiedId, MaterialRequirement requirement) in this.requirements)
        {
            int remaining = requirement.Required - requirement.GetSubmitted(this.data);
            if (remaining <= 0)
                continue;

            int available = deposited
                .Where(item => item?.QualifiedItemId == qualifiedId)
                .Sum(item => item?.Stack ?? 0);
            int amount = Math.Min(available, remaining);
            if (amount <= 0)
                continue;

            RemoveItemsFromList(deposited, item => item.QualifiedItemId == qualifiedId, amount);
            requirement.SetSubmitted(this.data, requirement.GetSubmitted(this.data) + amount);
            submitted.Add($"{requirement.DisplayName} ×{amount}");
        }

        foreach (Item item in deposited)
        {
            Item? leftover = Game1.player.addItemToInventory(item);
            if (leftover is not null)
                Game1.createItemDebris(leftover, Game1.player.Position, Game1.player.FacingDirection);
        }

        if (submitted.Count == 0)
        {
            Game1.drawObjectDialogue("你身上没有当前需要的治疗材料。\n\n" + this.GetProgressText());
            return;
        }

        if (this.data.HasAllMaterials)
        {
            this.data.Stage = QuestStage.MaterialsComplete;
            this.Helper.Data.WriteSaveData(SaveKey, this.data);
            Game1.drawObjectDialogue("你关上冰箱。哈维需要的材料已经全部集齐！\n\n在非节日的9:00—18:00去乔治家看看。");
            return;
        }

        this.Helper.Data.WriteSaveData(SaveKey, this.data);
        Game1.drawObjectDialogue("已提交：" + string.Join("、", submitted) + "。\n\n" + this.GetProgressText());
    }

    private string GetProgressText()
    {
        return $"治疗材料进度：\n兔子的脚：{this.data.RabbitFootSubmitted}/1";
    }

    private static void RemoveItemsFromList(List<Item> items, Func<Item, bool> predicate, int amount)
    {
        for (int index = items.Count - 1; index >= 0 && amount > 0; index--)
        {
            Item item = items[index];
            if (!predicate(item))
                continue;

            int removed = Math.Min(item.Stack, amount);
            item.Stack -= removed;
            amount -= removed;

            if (item.Stack <= 0)
                items.RemoveAt(index);
        }
    }

    private bool IsRefrigeratorInteraction(GameLocation location, Point target)
    {
        if (this.refrigeratorTile is Point fridge
            && Math.Abs(fridge.X - target.X) <= 1
            && Math.Abs(fridge.Y - target.Y) <= 1)
        {
            return true;
        }

        string? action = location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings");
        return IsRefrigeratorAction(action);
    }

    private Point? FindRefrigeratorTile(GameLocation location)
    {
        int width = location.Map.Layers[0].LayerWidth;
        int height = location.Map.Layers[0].LayerHeight;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                string? action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (IsRefrigeratorAction(action))
                {
                    this.Monitor.Log($"找到哈维房间冰箱交互格：({x}, {y})，Action={action}", LogLevel.Debug);
                    return new Point(x, y);
                }
            }
        }

        this.Monitor.Log("没有自动找到哈维房间的冰箱交互格。", LogLevel.Warn);
        return null;
    }

    private static bool IsRefrigeratorAction(string? action)
    {
        return action?.Contains(HarveyRefrigeratorAction, StringComparison.OrdinalIgnoreCase) == true
            || action?.Contains("fridge", StringComparison.OrdinalIgnoreCase) == true
            || action?.Contains("refrigerator", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void OnStatusCommand(string command, string[] args)
    {
        this.Monitor.Log(
            $"阶段：{this.data.Stage}；兔子的脚 {this.data.RabbitFootSubmitted}/1；当前地点：{Game1.currentLocation?.NameOrUniqueName ?? "无"}；冰箱格：{this.refrigeratorTile?.ToString() ?? "未找到"}",
            LogLevel.Info
        );
    }

    private void OnSetStageCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.Monitor.Log("请先读取一个存档。", LogLevel.Warn);
            return;
        }

        string value = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
        QuestStage? stage = value switch
        {
            "notstarted" => QuestStage.NotStarted,
            "collecting" => QuestStage.Collecting,
            "materials" => QuestStage.MaterialsComplete,
            "declined" => QuestStage.Declined,
            "completed" => QuestStage.Completed,
            _ => null
        };

        if (stage is null)
        {
            this.Monitor.Log("用法：george_recovery_setstage <notstarted|collecting|materials|declined|completed>", LogLevel.Warn);
            return;
        }

        Game1.player.mailReceived.Remove(AcceptedFlag);
        Game1.player.mailReceived.Remove(DeclinedFlag);
        Game1.player.mailReceived.Remove(CompletedFlag);
        this.openingSceneStarted = false;
        this.finaleSceneStarted = false;
        this.data = new SaveData { Stage = stage.Value };

        if (stage == QuestStage.Collecting || stage == QuestStage.MaterialsComplete)
            Game1.player.mailReceived.Add(AcceptedFlag);
        else if (stage == QuestStage.Declined)
            Game1.player.mailReceived.Add(DeclinedFlag);
        else if (stage == QuestStage.Completed)
            Game1.player.mailReceived.Add(CompletedFlag);

        if (stage == QuestStage.MaterialsComplete || stage == QuestStage.Completed)
            this.data.RabbitFootSubmitted = 1;

        this.Helper.Data.WriteSaveData(SaveKey, this.data);
        this.Monitor.Log($"测试阶段已设置为 {stage.Value}。", LogLevel.Info);
    }

    private sealed record MaterialRequirement(
        string DisplayName,
        int Required,
        Func<SaveData, int> GetSubmitted,
        Action<SaveData, int> SetSubmitted
    );
}
