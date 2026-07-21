namespace GeorgeRecovery;

internal sealed class SaveData
{
    public QuestStage Stage { get; set; } = QuestStage.NotStarted;
    public int RabbitFootSubmitted { get; set; }

    public bool HasAllMaterials => this.RabbitFootSubmitted >= 1;
}

internal enum QuestStage
{
    NotStarted,
    Collecting,
    MaterialsComplete,
    Declined,
    Completed
}
