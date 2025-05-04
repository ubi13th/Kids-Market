public class SmartContractBuilder
{
    private static SmartContractBuilder _instance;
    public static SmartContractBuilder Instance => _instance ??= new SmartContractBuilder();

    public SmartContractModel Current { get; private set; } = new SmartContractModel();

    public void SetTitleAndIcon(string title, string iconPath)
    {
        Current.Title = title;
        Current.IconPath = iconPath;
    }
    
    public void Reset()
    {
        Current = new SmartContractModel();
    }


    // More setters will follow as we build further steps
}