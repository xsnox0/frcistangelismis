using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Frcistan lisans anahtarı üretici.
// GİZLİ ANAHTAR (ozel-anahtar.txt) KİMSEYLE PAYLAŞILMAMALI, GitHub'a YÜKLENMEMELİDİR.
//
// Kullanım:
//   dotnet run -- "Firma Adı"                       süresiz Pro lisans
//   dotnet run -- "Firma Adı" --bitis 2027-12-31    bitiş tarihli lisans
//   dotnet run -- "Firma Adı" --tur Standart        lisans türü (varsayılan: Pro)
//   dotnet run -- --anahtar-uret                    yeni anahtar çifti (eski lisanslar geçersiz olur!)

Console.OutputEncoding = Encoding.UTF8;
string klasor = AppContext.BaseDirectory;
// dotnet run ile çalışınca proje klasörünü kullan
string projeKlasoru = Path.GetFullPath(Path.Combine(klasor, "..", "..", ".."));
if (File.Exists(Path.Combine(projeKlasoru, "LisansUretici.csproj"))) klasor = projeKlasoru;
string ozelDosya = Path.Combine(klasor, "ozel-anahtar.txt");

if (args.Length > 0 && args[0] == "--anahtar-uret")
{
    if (File.Exists(ozelDosya))
    {
        Console.WriteLine("Zaten bir gizli anahtar var: " + ozelDosya);
        Console.WriteLine("Yeni anahtar üretirseniz daha önce verdiğiniz bütün lisanslar geçersiz olur.");
        Console.WriteLine("Yine de istiyorsanız önce bu dosyayı silin.");
        return 1;
    }
    using var yeni = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(ozelDosya, Convert.ToBase64String(yeni.ExportPkcs8PrivateKey()));
    Console.WriteLine("Gizli anahtar kaydedildi: " + ozelDosya);
    Console.WriteLine();
    Console.WriteLine("Aşağıdaki açık anahtarı src/Frcistan/Servisler/LisansServisi.cs içindeki AcikAnahtar sabitine yapıştırın:");
    Console.WriteLine(Convert.ToBase64String(yeni.ExportSubjectPublicKeyInfo()));
    return 0;
}

if (args.Length == 0 || args[0].StartsWith("--"))
{
    Console.WriteLine("Kullanım: LisansUretici \"Firma Adı\" [--bitis yyyy-aa-gg] [--tur Pro]");
    return 1;
}

if (!File.Exists(ozelDosya))
{
    Console.WriteLine("Gizli anahtar bulunamadı: " + ozelDosya);
    Console.WriteLine("Önce 'LisansUretici --anahtar-uret' komutunu çalıştırın.");
    return 1;
}

string ad = args[0].Trim();
string tur = "Pro";
string bitis = null;
for (int i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "--bitis") bitis = DateTime.Parse(args[i + 1]).ToString("yyyy-MM-dd");
    if (args[i] == "--tur") tur = args[i + 1];
}

var yuk = new Dictionary<string, string>
{
    ["ad"] = ad,
    ["tur"] = tur,
    ["bitis"] = bitis,
    ["no"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)),
    ["tarih"] = DateTime.Today.ToString("yyyy-MM-dd")
};
byte[] veri = JsonSerializer.SerializeToUtf8Bytes(yuk);

using var ec = ECDsa.Create();
ec.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(ozelDosya).Trim()), out _);
byte[] imza = ec.SignData(veri, HashAlgorithmName.SHA256);

string anahtar = "FRC1." + B64(veri) + "." + B64(imza);
Console.WriteLine();
Console.WriteLine($"Lisans sahibi : {ad}");
Console.WriteLine($"Lisans türü   : {tur}");
Console.WriteLine($"Bitiş         : {(bitis ?? "Süresiz")}");
Console.WriteLine($"Lisans no     : {yuk["no"]}");
Console.WriteLine();
Console.WriteLine("Lisans anahtarı (müşteriye bunu gönderin):");
Console.WriteLine(anahtar);
Console.WriteLine();

File.AppendAllText(Path.Combine(klasor, "verilen-lisanslar.txt"),
    $"{DateTime.Now:yyyy-MM-dd HH:mm}\t{yuk["no"]}\t{ad}\t{tur}\t{bitis ?? "Süresiz"}\t{anahtar}{Environment.NewLine}");
return 0;

static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
