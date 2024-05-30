namespace ContentManager_Server
{
    public interface IClientMessageObserver
    {
        bool HandleMessageFromClient(string data);
        void Dispose();
    }
}