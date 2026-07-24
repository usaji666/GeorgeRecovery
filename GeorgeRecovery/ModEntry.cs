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
    private const string EvelynDialogueAsset = "Characters/Dialogue/Evelyn";
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
        ["(O)446"] = new("item.rabbit-foot", 1, data => data.RabbitFootSubmitted, (data, value) => data.RabbitFootSubmitted = value)
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
            this.T("command.status.description"),
            this.OnStatusCommand
        );
        helper.ConsoleCommands.Add(
            "george_recovery_setstage",
            this.T("command.setstage.description"),
            this.OnSetStageCommand
        );
    }

    private string T(string key)
    {
        return this.Helper.Translation.Get(key);
    }

    private string T(string key, object tokens)
    {
        return this.Helper.Translation.Get(key, tokens);
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
                    + $"speak Harvey \"{this.T("event.accepted.harvey-knew")}\"/"
                    + $"speak Harvey \"{this.T("event.accepted.request")}\"/"
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
                dialogue["Wed8"] = this.T("dialogue.george.wed8");
                dialogue["Tue10"] = this.T("dialogue.george.tue10");
            });
            return;
        }

        if (e.NameWithoutLocale.IsEquivalentTo(EvelynDialogueAsset)
            && Context.IsWorldReady
            && IsRecoveryComplete())
        {
            e.Edit(asset =>
            {
                IDictionary<string, string> dialogue = asset.AsDictionary<string, string>().Data;
                dialogue["Fri"] = this.T("dialogue.evelyn.fri");
                dialogue["summer_Tue"] = this.T("dialogue.evelyn.summer-tue");
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
        this.RestoreCompletionFlagFromSaveData();
        this.lastRecoveryComplete = IsRecoveryComplete();
        this.InvalidateRecoveryDialogue();
        this.Monitor.Log(
            this.T(
                "log.save-loaded",
                new
                {
                    stage = this.data.Stage,
                    completed = Game1.player.mailReceived.Contains(CompletedFlag),
                    time = Game1.timeOfDay
                }
            ),
            LogLevel.Debug
        );
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.SyncStageFromMailFlags();
        this.lastRecoveryComplete = IsRecoveryComplete();
        this.InvalidateRecoveryDialogue();

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
        if (recoveryComplete != this.lastRecoveryComplete && !Game1.eventUp)
        {
            this.lastRecoveryComplete = recoveryComplete;
            this.InvalidateRecoveryDialogue();

            if (recoveryComplete)
                this.ApplyRecoveryScheduleForToday();
            else
                this.RestoreGeorgeSchedule();
        }

        this.UpdateGeorgeAppearance();
    }

    private bool IsRecoveryComplete()
    {
        return Context.IsWorldReady
            && (this.data.Stage == QuestStage.Completed
                || Game1.player.mailReceived.Contains(CompletedFlag));
    }

    private void RestoreCompletionFlagFromSaveData()
    {
        if (this.data.Stage == QuestStage.Completed
            && !Game1.player.mailReceived.Contains(CompletedFlag))
        {
            Game1.player.mailReceived.Add(CompletedFlag);
            this.Monitor.Log(this.T("log.completion-restored"), LogLevel.Debug);
        }
    }

    private void MarkRecoveryComplete()
    {
        this.data.Stage = QuestStage.Completed;
        this.data.RabbitFootSubmitted = Math.Max(this.data.RabbitFootSubmitted, 1);
        if (!Game1.player.mailReceived.Contains(CompletedFlag))
            Game1.player.mailReceived.Add(CompletedFlag);

        this.Helper.Data.WriteSaveData(SaveKey, this.data);
        this.InvalidateRecoveryDialogue();
        this.Monitor.Log(this.T("log.completion-written"), LogLevel.Debug);
    }

    private void InvalidateRecoveryDialogue()
    {
        this.Helper.GameContent.InvalidateCache(GeorgeDialogueAsset);
        this.Helper.GameContent.InvalidateCache(EvelynDialogueAsset);
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
            this.Monitor.Log(this.T("log.schedule-loaded", new { schedule = scheduleKey }), LogLevel.Debug);
        else
            this.Monitor.Log(this.T("log.schedule-failed", new { schedule = scheduleKey }), LogLevel.Warn);
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
            + $"speak George \"{this.T("event.opening.george-rain")}\"/"
            + $"speak George \"{this.T("event.opening.george-rehab")}\"/"
            + $"speak George \"{this.T("event.opening.george-evelyn")}\"/"
            + "pause 1000/"
            + "addTemporaryActor Harvey 16 32 13 21 1 true Character Harvey/playSound doorClose/"
            + "ignoreCollisions Harvey/"
            + "advancedMove Harvey false 0 1 3 0/proceedPosition Harvey/stopAdvancedMoves/"
            + "faceDirection Harvey 0/faceDirection farmer 1/faceDirection George 3/pause 300/"
            + $"speak Harvey \"{this.T("event.opening.harvey-condition")}\"/"
            + $"speak Harvey \"{this.T("event.opening.harvey-request")}\"/"
            + $"question fork0 \"{this.T("event.opening.question")}#{this.T("event.opening.answer-yes")}#{this.T("event.opening.answer-no")}\"/"
            + $"fork {AcceptedBranchId}/"
            + "friendship Harvey -50/friendship George -50/emote Harvey 12/"
            + $"speak Harvey \"{this.T("event.opening.declined")}\"/"
            + $"mailReceived {DeclinedFlag}/end";

        location.startEvent(new Event(script, Game1.player));
    }

    private void StartFinaleScene(GameLocation location)
    {
        // Mark completion before the event starts, so skipping or warping away
        // can't prevent the 12:00—17:00 standing schedule from unlocking.
        this.MarkRecoveryComplete();

        string script =
            "none/-1000 -1000/farmer 14 21 1 George 17 21 3/"
            + "skippable/viewport 16 21 true/pause 500/"
            + "move farmer 2 0 1/faceDirection George 3/pause 300/"
            + "emote George 20/"
            + $"speak George \"{this.T("event.finale.george-doctor")}\"/"
            + $"speak George \"{this.T("event.finale.george-thanks")}\"/"
            + $"speak George \"{this.T("event.finale.george-future")}\"/"
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
            message: this.T("menu.material-box"),
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
            submitted.Add(
                this.T(
                    "item.submitted-count",
                    new
                    {
                        item = this.T(requirement.DisplayNameKey),
                        count = amount
                    }
                )
            );
        }

        foreach (Item item in deposited)
        {
            Item? leftover = Game1.player.addItemToInventory(item);
            if (leftover is not null)
                Game1.createItemDebris(leftover, Game1.player.Position, Game1.player.FacingDirection);
        }

        if (submitted.Count == 0)
        {
            Game1.drawObjectDialogue(
                this.T("materials.none", new { progress = this.GetProgressText() })
            );
            return;
        }

        if (this.data.HasAllMaterials)
        {
            this.data.Stage = QuestStage.MaterialsComplete;
            this.Helper.Data.WriteSaveData(SaveKey, this.data);
            Game1.drawObjectDialogue(this.T("materials.complete"));
            return;
        }

        this.Helper.Data.WriteSaveData(SaveKey, this.data);
        Game1.drawObjectDialogue(
            this.T(
                "materials.submitted",
                new
                {
                    items = string.Join(this.T("list.separator"), submitted),
                    progress = this.GetProgressText()
                }
            )
        );
    }

    private string GetProgressText()
    {
        return this.T(
            "materials.progress",
            new
            {
                current = this.data.RabbitFootSubmitted,
                required = 1
            }
        );
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
                    this.Monitor.Log(
                        this.T(
                            "log.fridge-found",
                            new
                            {
                                x,
                                y,
                                action = action ?? ""
                            }
                        ),
                        LogLevel.Debug
                    );
                    return new Point(x, y);
                }
            }
        }

        this.Monitor.Log(this.T("log.fridge-not-found"), LogLevel.Warn);
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
            this.T(
                "log.status",
                new
                {
                    stage = this.data.Stage,
                    submitted = this.data.RabbitFootSubmitted,
                    required = 1,
                    location = Game1.currentLocation?.NameOrUniqueName ?? this.T("common.none"),
                    fridge = this.refrigeratorTile?.ToString() ?? this.T("common.not-found")
                }
            ),
            LogLevel.Info
        );
    }

    private void OnSetStageCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.Monitor.Log(this.T("log.load-save-first"), LogLevel.Warn);
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
            this.Monitor.Log(this.T("log.setstage-usage"), LogLevel.Warn);
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
        this.InvalidateRecoveryDialogue();
        this.Monitor.Log(this.T("log.stage-set", new { stage = stage.Value }), LogLevel.Info);
    }

    private sealed record MaterialRequirement(
        string DisplayNameKey,
        int Required,
        Func<SaveData, int> GetSubmitted,
        Action<SaveData, int> SetSubmitted
    );
}
