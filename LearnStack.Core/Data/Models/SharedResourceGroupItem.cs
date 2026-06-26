namespace LearnStack.Data.Models;

public class SharedResourceGroupItem
{
    public int SharedResourceGroupId { get; set; }
    public SharedResourceGroup? SharedResourceGroup { get; set; }

    public int LearningResourceId { get; set; }
    public LearningResource? LearningResource { get; set; }
}
