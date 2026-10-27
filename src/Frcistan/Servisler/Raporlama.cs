using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Frcistan.Altyapi;

namespace Frcistan.Servisler
{
    public enum SutunTuru { Metin, Tamsayi, Para, Tarih, Yuzde }

    public class RaporSutunu
    {
        public string Baslik { get; init; }
        public SutunTuru Tur { get; init; } = SutunTuru.Metin;

        /// <summary>Excel sütun genişliği (karakter)</summary>
        public double Genislik { get; init; } = 14;

        /// <summary>Yazdırırken göreli genişlik</summary>
        public double Oran { get; init; } = 1;

        public RaporSutunu(string baslik, SutunTuru tur = SutunTuru.Metin, double genislik = 14, double oran = 1)
        {
            Baslik = baslik; Tur = tur; Genislik = genislik; Oran = oran;
        }

        public bool SagaYasli => Tur != SutunTuru.Metin;

        public string Bicimle(object deger) => deger switch
        {
            null => "",
            decimal d when Tur == SutunTuru.Para => Para.Bicimle(d),
            decimal d when Tur == SutunTuru.Yuzde => "%" + (d * 100).ToString("0.#", Ui.Tr),
            int i => i.ToString("N0", Ui.Tr),
            decimal d => d.ToString("N2", Ui.Tr),
            DateTime t => t.ToString("dd.MM.yyyy HH:mm", Ui.Tr),
            _ => deger.ToString()
        };
    }

    /// <summary>Excel'e aktarılabilen ve yazdırılabilen tablo</summary>
    public class Rapor
    {
        public string Baslik { get; set; }
        public string AltBaslik { get; set; }
        public string SayfaAdi { get; set; } = "Rapor";
        public string DosyaAdi { get; set; } = "Rapor";
        public bool Yatay { get; set; }
        public List<RaporSutunu> Sutunlar { get; } = new();
        public List<object[]> Satirlar { get; } = new();
        public List<(string Etiket, string Deger)> Ozet { get; } = new();

        public Rapor Sutun(string baslik, SutunTuru tur = SutunTuru.Metin, double genislik = 14, double oran = 1)
        {
            Sutunlar.Add(new RaporSutunu(baslik, tur, genislik, oran));
            return this;
        }

        public void Satir(params object[] degerler) => Satirlar.Add(degerler);
    }

    /// <summary>Ek kütüphane kullanmadan gerçek .xlsx (Office Open XML) dosyası üretir.</summary>
    public static class ExcelYazici
    {
        const string AnaNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Yaz(string dosya, params Rapor[] raporlar)
        {
            if (File.Exists(dosya)) File.Delete(dosya);
            using var zip = ZipFile.Open(dosya, ZipArchiveMode.Create);

            Parca(zip, "[Content_Types].xml", w =>
            {
                w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                Varsayilan(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                Varsayilan(w, "xml", "application/xml");
                Gecersiz(w, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                Gecersiz(w, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                Gecersiz(w, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                for (int i = 0; i < raporlar.Length; i++)
                    Gecersiz(w, $"/xl/worksheets/sheet{i + 1}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                w.WriteEndElement();
            });

            Parca(zip, "_rels/.rels", w =>
            {
                w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                Iliski(w, "rId1", RelNs + "/officeDocument", "xl/workbook.xml");
                Iliski(w, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
                w.WriteEndElement();
            });

            Parca(zip, "docProps/core.xml", w =>
            {
                w.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                w.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                w.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                w.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                w.WriteElementString("dc", "title", "http://purl.org/dc/elements/1.1/", raporlar[0].Baslik);
                w.WriteElementString("dc", "creator", "http://purl.org/dc/elements/1.1/", "Frcistan");
                w.WriteStartElement("dcterms", "created", "http://purl.org/dc/terms/");
                w.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "dcterms:W3CDTF");
                w.WriteString(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                w.WriteEndElement();
                w.WriteEndElement();
            });

            var adlar = SayfaAdlari(raporlar);
            Parca(zip, "xl/workbook.xml", w =>
            {
                w.WriteStartElement("workbook", AnaNs);
                w.WriteAttributeString("xmlns", "r", null, RelNs);
                w.WriteStartElement("sheets", AnaNs);
                for (int i = 0; i < raporlar.Length; i++)
                {
                    w.WriteStartElement("sheet", AnaNs);
                    w.WriteAttributeString("name", adlar[i]);
                    w.WriteAttributeString("sheetId", (i + 1).ToString());
                    w.WriteAttributeString("id", RelNs, "rId" + (i + 1));
                    w.WriteEndElement();
                }
                w.WriteEndElement();
                w.WriteStartElement("definedNames", AnaNs);
                for (int i = 0; i < raporlar.Length; i++)
                {
                    var r = raporlar[i];
                    w.WriteStartElement("definedName", AnaNs);
                    w.WriteAttributeString("name", "_xlnm._FilterDatabase");
                    w.WriteAttributeString("localSheetId", i.ToString());
                    w.WriteAttributeString("hidden", "1");
                    w.WriteString($"'{adlar[i].Replace("'", "''")}'!$A$4:${Harf(r.Sutunlar.Count)}${4 + Math.Max(1, r.Satirlar.Count)}");
                    w.WriteEndElement();
                }
                w.WriteEndElement();
                w.WriteEndElement();
            });

            Parca(zip, "xl/_rels/workbook.xml.rels", w =>
            {
                w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                for (int i = 0; i < raporlar.Length; i++)
                    Iliski(w, "rId" + (i + 1), RelNs + "/worksheet", $"worksheets/sheet{i + 1}.xml");
                Iliski(w, "rId" + (raporlar.Length + 1), RelNs + "/styles", "styles.xml");
                w.WriteEndElement();
            });

            Parca(zip, "xl/styles.xml", Stiller);

            for (int i = 0; i < raporlar.Length; i++)
            {
                var r = raporlar[i];
                Parca(zip, $"xl/worksheets/sheet{i + 1}.xml", w => Sayfa(w, r));
            }
        }

        // Stil numaraları: 0 normal, 1 başlık satırı, 2 para, 3 tamsayı, 4 tarih, 5 büyük başlık, 6 alt başlık, 7 yüzde, 8 özet etiketi, 9 özet değeri
        static void Stiller(XmlWriter w)
        {
            w.WriteStartElement("styleSheet", AnaNs);

            w.WriteStartElement("numFmts", AnaNs);
            w.WriteAttributeString("count", "1");
            w.WriteStartElement("numFmt", AnaNs);
            w.WriteAttributeString("numFmtId", "164");
            w.WriteAttributeString("formatCode", "dd.mm.yyyy hh:mm");
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("fonts", AnaNs);
            w.WriteAttributeString("count", "5");
            Yazi(w, 11, false, null);
            Yazi(w, 11, true, "FFFFFFFF");
            Yazi(w, 16, true, "FF0F172A");
            Yazi(w, 10, false, "FF64748B");
            Yazi(w, 11, true, "FF0F172A");
            w.WriteEndElement();

            w.WriteStartElement("fills", AnaNs);
            w.WriteAttributeString("count", "3");
            Dolgu(w, "none", null);
            Dolgu(w, "gray125", null);
            Dolgu(w, "solid", "FF1E3A8A");
            w.WriteEndElement();

            w.WriteStartElement("borders", AnaNs);
            w.WriteAttributeString("count", "1");
            w.WriteStartElement("border", AnaNs);
            foreach (var k in new[] { "left", "right", "top", "bottom", "diagonal" }) { w.WriteStartElement(k, AnaNs); w.WriteEndElement(); }
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("cellStyleXfs", AnaNs);
            w.WriteAttributeString("count", "1");
            Xf(w, 0, 0, 0, false);
            w.WriteEndElement();

            w.WriteStartElement("cellXfs", AnaNs);
            w.WriteAttributeString("count", "10");
            Xf(w, 0, 0, 0, true);    // 0
            Xf(w, 0, 1, 2, true);    // 1 başlık
            Xf(w, 4, 0, 0, true);    // 2 para (#,##0.00)
            Xf(w, 3, 0, 0, true);    // 3 tamsayı (#,##0)
            Xf(w, 164, 0, 0, true);  // 4 tarih
            Xf(w, 0, 2, 0, true);    // 5 büyük başlık
            Xf(w, 0, 3, 0, true);    // 6 alt başlık
            Xf(w, 10, 0, 0, true);   // 7 yüzde (0.00%)
            Xf(w, 0, 4, 0, true);    // 8 özet etiketi
            Xf(w, 0, 4, 0, true);    // 9 özet değeri
            w.WriteEndElement();

            w.WriteStartElement("cellStyles", AnaNs);
            w.WriteAttributeString("count", "1");
            w.WriteStartElement("cellStyle", AnaNs);
            w.WriteAttributeString("name", "Normal");
            w.WriteAttributeString("xfId", "0");
            w.WriteAttributeString("builtinId", "0");
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteEndElement();
        }

        static void Sayfa(XmlWriter w, Rapor r)
        {
            int sutunSayisi = Math.Max(1, r.Sutunlar.Count);
            w.WriteStartElement("worksheet", AnaNs);
            w.WriteAttributeString("xmlns", "r", null, RelNs);

            w.WriteStartElement("sheetViews", AnaNs);
            w.WriteStartElement("sheetView", AnaNs);
            w.WriteAttributeString("workbookViewId", "0");
            w.WriteAttributeString("showGridLines", "0");
            w.WriteStartElement("pane", AnaNs);
            w.WriteAttributeString("ySplit", "4");
            w.WriteAttributeString("topLeftCell", "A5");
            w.WriteAttributeString("activePane", "bottomLeft");
            w.WriteAttributeString("state", "frozen");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("sheetFormatPr", AnaNs);
            w.WriteAttributeString("defaultRowHeight", "16");
            w.WriteEndElement();

            w.WriteStartElement("cols", AnaNs);
            for (int i = 0; i < r.Sutunlar.Count; i++)
            {
                w.WriteStartElement("col", AnaNs);
                w.WriteAttributeString("min", (i + 1).ToString());
                w.WriteAttributeString("max", (i + 1).ToString());
                w.WriteAttributeString("width", r.Sutunlar[i].Genislik.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("customWidth", "1");
                w.WriteEndElement();
            }
            w.WriteEndElement();

            w.WriteStartElement("sheetData", AnaNs);
            SatirYaz(w, 1, 26, new[] { (object)r.Baslik }, new[] { 5 });
            SatirYaz(w, 2, null, new[] { (object)$"{r.AltBaslik}" }, new[] { 6 });
            SatirYaz(w, 3, 8, Array.Empty<object>(), Array.Empty<int>());
            SatirYaz(w, 4, 22, r.Sutunlar.Select(s => (object)s.Baslik).ToArray(), r.Sutunlar.Select(_ => 1).ToArray());

            int no = 5;
            foreach (var satir in r.Satirlar)
            {
                var stiller = new int[sutunSayisi];
                for (int i = 0; i < r.Sutunlar.Count; i++)
                    stiller[i] = r.Sutunlar[i].Tur switch
                    {
                        SutunTuru.Para => 2,
                        SutunTuru.Tamsayi => 3,
                        SutunTuru.Tarih => 4,
                        SutunTuru.Yuzde => 7,
                        _ => 0
                    };
                SatirYaz(w, no++, null, satir, stiller);
            }

            if (r.Ozet.Count > 0)
            {
                no++;
                foreach (var (etiket, deger) in r.Ozet)
                    SatirYaz(w, no++, null, new object[] { etiket, deger }, new[] { 8, 9 });
            }
            w.WriteEndElement(); // sheetData

            w.WriteStartElement("autoFilter", AnaNs);
            w.WriteAttributeString("ref", $"A4:{Harf(sutunSayisi)}{4 + Math.Max(1, r.Satirlar.Count)}");
            w.WriteEndElement();

            w.WriteStartElement("pageMargins", AnaNs);
            foreach (var (k, v) in new[] { ("left", "0.5"), ("right", "0.5"), ("top", "0.6"), ("bottom", "0.6"), ("header", "0.3"), ("footer", "0.3") })
                w.WriteAttributeString(k, v);
            w.WriteEndElement();

            w.WriteStartElement("pageSetup", AnaNs);
            w.WriteAttributeString("paperSize", "9");
            w.WriteAttributeString("orientation", r.Yatay ? "landscape" : "portrait");
            w.WriteAttributeString("fitToWidth", "1");
            w.WriteAttributeString("fitToHeight", "0");
            w.WriteEndElement();

            w.WriteEndElement(); // worksheet
        }

        static void SatirYaz(XmlWriter w, int no, double? yukseklik, object[] degerler, int[] stiller)
        {
            w.WriteStartElement("row", AnaNs);
            w.WriteAttributeString("r", no.ToString());
            if (yukseklik.HasValue)
            {
                w.WriteAttributeString("ht", yukseklik.Value.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("customHeight", "1");
            }
            for (int i = 0; i < degerler.Length; i++)
            {
                var d = degerler[i];
                if (d == null) continue;
                int stil = i < stiller.Length ? stiller[i] : 0;
                w.WriteStartElement("c", AnaNs);
                w.WriteAttributeString("r", Harf(i + 1) + no);
                if (stil != 0) w.WriteAttributeString("s", stil.ToString());

                switch (d)
                {
                    case int or long or decimal or double or float:
                        w.WriteElementString("v", AnaNs, Convert.ToDecimal(d).ToString(CultureInfo.InvariantCulture));
                        break;
                    case DateTime t:
                        w.WriteElementString("v", AnaNs, t.ToOADate().ToString(CultureInfo.InvariantCulture));
                        break;
                    default:
                        w.WriteAttributeString("t", "inlineStr");
                        w.WriteStartElement("is", AnaNs);
                        w.WriteStartElement("t", AnaNs);
                        w.WriteAttributeString("xml", "space", null, "preserve");
                        w.WriteString(Temizle(d.ToString()));
                        w.WriteEndElement();
                        w.WriteEndElement();
                        break;
                }
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        static string[] SayfaAdlari(Rapor[] raporlar)
        {
            var adlar = new List<string>();
            foreach (var r in raporlar)
            {
                string ad = new string((r.SayfaAdi ?? "Sayfa").Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
                if (ad.Length == 0) ad = "Sayfa";
                if (ad.Length > 28) ad = ad[..28];
                string aday = ad; int n = 2;
                while (adlar.Contains(aday, StringComparer.OrdinalIgnoreCase)) aday = $"{ad} {n++}";
                adlar.Add(aday);
            }
            return adlar.ToArray();
        }

        static string Temizle(string s) => new(s.Where(c => c == '\t' || c == '\n' || c == '\r' || c >= ' ').ToArray());

        public static string Harf(int sutun)
        {
            string s = "";
            while (sutun > 0) { int m = (sutun - 1) % 26; s = (char)('A' + m) + s; sutun = (sutun - 1) / 26; }
            return s;
        }

        static void Parca(ZipArchive zip, string ad, Action<XmlWriter> yaz)
        {
            var giris = zip.CreateEntry(ad, CompressionLevel.Optimal);
            using var akis = giris.Open();
            using var w = XmlWriter.Create(akis, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
            w.WriteStartDocument(true);
            yaz(w);
            w.WriteEndDocument();
        }

        static void Varsayilan(XmlWriter w, string uzanti, string tur)
        {
            w.WriteStartElement("Default", "http://schemas.openxmlformats.org/package/2006/content-types");
            w.WriteAttributeString("Extension", uzanti);
            w.WriteAttributeString("ContentType", tur);
            w.WriteEndElement();
        }

        static void Gecersiz(XmlWriter w, string parca, string tur)
        {
            w.WriteStartElement("Override", "http://schemas.openxmlformats.org/package/2006/content-types");
            w.WriteAttributeString("PartName", parca);
            w.WriteAttributeString("ContentType", tur);
            w.WriteEndElement();
        }

        static void Iliski(XmlWriter w, string id, string tur, string hedef)
        {
            w.WriteStartElement("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");
            w.WriteAttributeString("Id", id);
            w.WriteAttributeString("Type", tur);
            w.WriteAttributeString("Target", hedef);
            w.WriteEndElement();
        }

        static void Yazi(XmlWriter w, double boyut, bool kalin, string renk)
        {
            w.WriteStartElement("font", AnaNs);
            if (kalin) { w.WriteStartElement("b", AnaNs); w.WriteEndElement(); }
            w.WriteStartElement("sz", AnaNs); w.WriteAttributeString("val", boyut.ToString(CultureInfo.InvariantCulture)); w.WriteEndElement();
            if (renk != null) { w.WriteStartElement("color", AnaNs); w.WriteAttributeString("rgb", renk); w.WriteEndElement(); }
            w.WriteStartElement("name", AnaNs); w.WriteAttributeString("val", "Calibri"); w.WriteEndElement();
            w.WriteStartElement("family", AnaNs); w.WriteAttributeString("val", "2"); w.WriteEndElement();
            w.WriteEndElement();
        }

        static void Dolgu(XmlWriter w, string desen, string renk)
        {
            w.WriteStartElement("fill", AnaNs);
            w.WriteStartElement("patternFill", AnaNs);
            w.WriteAttributeString("patternType", desen);
            if (renk != null)
            {
                w.WriteStartElement("fgColor", AnaNs); w.WriteAttributeString("rgb", renk); w.WriteEndElement();
                w.WriteStartElement("bgColor", AnaNs); w.WriteAttributeString("indexed", "64"); w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        }

        static void Xf(XmlWriter w, int sayiBicimi, int yazi, int dolgu, bool hucre)
        {
            w.WriteStartElement("xf", AnaNs);
            w.WriteAttributeString("numFmtId", sayiBicimi.ToString());
            w.WriteAttributeString("fontId", yazi.ToString());
            w.WriteAttributeString("fillId", dolgu.ToString());
            w.WriteAttributeString("borderId", "0");
            if (hucre)
            {
                w.WriteAttributeString("xfId", "0");
                if (sayiBicimi != 0) w.WriteAttributeString("applyNumberFormat", "1");
                if (yazi != 0) w.WriteAttributeString("applyFont", "1");
                if (dolgu != 0) w.WriteAttributeString("applyFill", "1");
                if (dolgu != 0)
                {
                    w.WriteAttributeString("applyAlignment", "1");
                    w.WriteStartElement("alignment", AnaNs);
                    w.WriteAttributeString("vertical", "center");
                    w.WriteEndElement();
                }
            }
            w.WriteEndElement();
        }
    }
}
