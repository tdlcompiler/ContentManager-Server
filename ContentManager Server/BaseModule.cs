namespace ContentManager_Server
{
    public class BaseModule
    {
        public string ModuleName { get; }

        public BaseModule(string? moduleName = null)
        {
            ModuleName = moduleName ?? "unknown";
        }
    }
}
