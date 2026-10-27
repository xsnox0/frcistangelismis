namespace Frcistan.Altyapi
{
    public static class Surum
    {
        public static Version Tam => typeof(Surum).Assembly.GetName().Version ?? new Version(1, 0);

        /// <summary>"1.0"</summary>
        public static string Kisa => Tam.ToString(2);

        public const string Gelistirici = "İlyas Enes Sepetci";
    }
}
