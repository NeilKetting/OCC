namespace OCC.WpfClient.Infrastructure.Messages
{
    public enum AuthSide
    {
        Login,
        Register,
        ForgotPassword
    }

    public class AuthFlipMessage
    {
        public AuthSide Side { get; }
        public AuthFlipMessage(AuthSide side)
        {
            Side = side;
        }
    }
}
