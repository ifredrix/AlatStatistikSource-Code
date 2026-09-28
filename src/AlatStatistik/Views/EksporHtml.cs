using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public static class EksporHtml
    {

        public static string Buat(List<ResultBlock> blok, string judul = "Hasil analisis",
                                  string namaData = "")
        {
            var isi = new StringBuilder();

            foreach (var b in blok)
            {
                switch (b.Kind)
                {
                    case BlockKind.Heading: isi.Append(Judul(b)); break;
                    case BlockKind.Table: isi.Append(Tabel(b)); break;
                    case BlockKind.Note: isi.Append(Catatan(b)); break;
                    case BlockKind.Chart: isi.Append(Grafik(b)); break;
                    case BlockKind.Rumus: isi.Append(Rumus(b)); break;
                    case BlockKind.Substitusi: isi.Append(Substitusi(b)); break;
                }
            }

            string cap = DateTime.Now.ToString("dd MMMM yyyy, HH:mm", new System.Globalization.CultureInfo("id-ID"));
            string sumber = string.IsNullOrWhiteSpace(namaData)
                ? "" : $" · Data: {Esc(namaData)}";

            return $@"<!DOCTYPE html>
<html lang=""id"">
<head>
<meta charset=""utf-8""/>
<title>{Esc(judul)}</title>
<style>
  :root {{
    --teks: #16213A; --redup: #5A6478; --garis: #D7DDE8;
    --aksen: #0EA5E9; --latar: #FFFFFF; --panel: #F6F8FB;
  }}
  * {{ box-sizing: border-box; }}
  body {{
    margin: 0; padding: 32px 24px 64px;
    font-family: Segoe UI, Calibri, sans-serif; color: var(--teks);
    background: var(--panel); line-height: 1.55;
  }}
  .kertas {{
    max-width: 940px; margin: 0 auto; background: var(--latar);
    border: 1px solid var(--garis); border-radius: 10px; padding: 32px 36px 48px;
  }}
  h1 {{ font-size: 22px; margin: 0 0 4px; font-weight: 600; }}
  .cap {{ color: var(--redup); font-size: 12px; margin: 0 0 24px; }}
  h2 {{ font-size: 17px; margin: 26px 0 8px; font-weight: 600; }}
  h3 {{ font-size: 14px; margin: 18px 0 6px; font-weight: 600; color: var(--redup); }}
  table {{ border-collapse: collapse; width: 100%; margin: 6px 0 4px; font-size: 13px; }}
  th, td {{ border: 1px solid var(--garis); padding: 6px 9px; text-align: right; }}
  th:first-child, td:first-child {{ text-align: left; }}
  th {{ background: var(--panel); font-weight: 600; }}
  tbody tr:nth-child(even) {{ background: #FAFBFD; }}
  .catatan-kaki {{ font-size: 11.5px; color: var(--redup); margin: 2px 0 12px; }}
  .catatan {{
    border-left: 3px solid var(--aksen); background: var(--panel);
    padding: 8px 12px; margin: 8px 0 14px; font-size: 13px; white-space: pre-wrap;
  }}
  .catatan.ok {{ border-color: #10B981; }}
  .catatan.warning {{ border-color: #D97706; }}
  .catatan.error {{ border-color: #DC2626; }}
  .rumus {{
    border: 1px solid var(--garis); border-radius: 8px; padding: 12px 14px;
    margin: 8px 0 14px; background: var(--panel);
  }}
  .rumus .bentuk {{
    font-family: Consolas, monospace; font-size: 14px; margin: 4px 0 6px;
  }}
  .rumus .asal {{ font-size: 11.5px; color: var(--redup); }}
  .rumus .jenis {{
    display: inline-block; font-size: 11px; background: #E6F4FE; color: #075985;
    border-radius: 10px; padding: 1px 9px; margin-right: 6px;
  }}
  .langkah {{ font-family: Consolas, monospace; font-size: 13px; margin: 2px 0 2px 0; }}
  .uraian {{ font-size: 12px; color: var(--redup); margin: 8px 0 2px; }}
  .grafik {{ margin: 10px 0 18px; border: 1px solid var(--garis); border-radius: 8px; padding: 8px; overflow-x: auto; }}
  .grafik svg {{ display: block; max-width: 100%; height: auto; }}
  .kosong {{ color: var(--redup); font-size: 13px; }}
  .cara {{
    border: 1px solid var(--garis); border-radius: 8px; padding: 10px 14px 6px;
    margin: 0 0 18px; background: var(--panel);
  }}
  .cara .kepala {{ font-weight: 600; font-size: 12.5px; margin-bottom: 7px; }}
  .cara .bagian {{ margin: 0 0 8px; }}
  .cara .label {{ font-size: 11.5px; font-weight: 600; }}
  .cara .label.aksen {{ color: var(--aksen); }}
  .cara .label.ingat {{ color: #D97706; }}
  .cara .isi {{
    font-size: 12.5px; margin: 2px 0 0 10px; padding-left: 9px;
    border-left: 3px solid var(--garis);
  }}
  @media print {{
    body {{ background: #FFF; padding: 0; }}
    .kertas {{ border: 0; max-width: none; padding: 0; }}
  }}
</style>
</head>
<body>
<div class=""kertas"">
<h1>{Esc(judul)}</h1>
<p class=""cap"">Dihasilkan AlatStatistik · {Esc(cap)}{Esc(sumber)}</p>
{isi}
</div>
</body>
</html>";
        }

        public static string Simpan(string jalur, List<ResultBlock> blok,
                                    string judul = "Hasil analisis", string namaData = "")
        {
            File.WriteAllText(jalur, Buat(blok, judul, namaData), new UTF8Encoding(false));
            return jalur;
        }

        

        private static string Judul(ResultBlock b)
        {
            int level = b.Level <= 1 ? 2 : 3;
            return $"<h{level}>{Esc(b.Text)}</h{level}>";
        }

        private static string Tabel(ResultBlock b)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append($"<h3>{Esc(b.Title)}</h3>");

            if (b.Columns.Count == 0 && b.Rows.Count == 0)
                return sb.Append("<p class=\"kosong\">Tabel kosong.</p>").ToString();

            sb.Append("<table><thead><tr>");
            foreach (var c in b.Columns) sb.Append($"<th>{Esc(c)}</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var baris in b.Rows)
            {
                sb.Append("<tr>");
                foreach (var sel in baris) sb.Append($"<td>{Esc(sel ?? "")}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");

            if (!string.IsNullOrWhiteSpace(b.Footnote))
                sb.Append($"<p class=\"catatan-kaki\">{Esc(b.Footnote)}</p>");
            return sb.ToString();
        }

        private static string Catatan(ResultBlock b)
        {
            string kelas = b.Note switch
            {
                NoteKind.Ok => "ok",
                NoteKind.Warning => "warning",
                NoteKind.Error => "error",
                _ => ""
            };
            return $"<div class=\"catatan {kelas}\">{Esc(b.Text)}</div>";
        }

        private static string Grafik(ResultBlock b)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append($"<h3>{Esc(b.Title)}</h3>");

            if (b.Chart == null)
                return sb.Append("<p class=\"kosong\">Grafik tidak tersedia.</p>").ToString();

            string svg = GrafikSvg.Buat(b.Chart);
            if (svg.Length == 0)
                return sb.Append("<p class=\"kosong\">Grafik ini tidak dapat digambar "
                               + "(datanya kosong atau terlalu kecil).</p>").ToString();

            sb.Append("<div class=\"grafik\">").Append(svg).Append("</div>");

            
            
            sb.Append(CaraMembaca(b.Chart));
            return sb.ToString();
        }

        private static string CaraMembaca(ChartSpec spec)
        {
            var cara = PenjelasanGrafik.Untuk(spec.Kind);
            if (cara == null) return "";

            var sb = new StringBuilder();
            sb.Append("<div class=\"cara\">");
            sb.Append("<div class=\"kepala\">Cara membaca diagram ini</div>");
            sb.Append(BagianCara("Apa yang dibaca", cara.Bacaan, "aksen"));
            sb.Append(BagianCara("Yang perlu diperhatikan", cara.Perhatikan, "aksen"));
            sb.Append(BagianCara("Hati-hati", cara.HatiHati, "ingat"));
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string BagianCara(string label, string teks, string kelas)
        {
            if (string.IsNullOrWhiteSpace(teks)) return "";
            return $"<div class=\"bagian\"><div class=\"label {kelas}\">{Esc(label)}</div>"
                 + $"<div class=\"isi\">{Esc(teks)}</div></div>";
        }

        private static string Rumus(ResultBlock b)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append($"<h3>{Esc(b.Title)}</h3>");

            if (b.RumusList.Count == 0)
                return sb.Append("<p class=\"kosong\">Tidak ada rumus.</p>").ToString();

            foreach (var r in b.RumusList)
            {
                sb.Append("<div class=\"rumus\">");
                sb.Append($"<div><strong>{Esc(r.Nama)}</strong></div>");
                if (!string.IsNullOrWhiteSpace(r.Bentuk))
                    sb.Append($"<div class=\"bentuk\">{Esc(r.Bentuk)}</div>");
                sb.Append("<div class=\"asal\">");
                if (!string.IsNullOrWhiteSpace(r.Jenis))
                    sb.Append($"<span class=\"jenis\">{Esc(r.Jenis)}</span>");
                sb.Append(Esc(r.LabelTahun));
                sb.Append("</div>");
                if (!string.IsNullOrWhiteSpace(r.Catatan))
                    sb.Append($"<div class=\"catatan-kaki\">{Esc(r.Catatan)}</div>");
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        private static string Substitusi(ResultBlock b)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append($"<h3>{Esc(b.Title)}</h3>");

            if (b.Langkah.Count == 0)
                return sb.Append("<p class=\"kosong\">Tidak ada perhitungan.</p>").ToString();

            foreach (var l in b.Langkah)
            {
                if (!string.IsNullOrWhiteSpace(l.Uraian))
                    sb.Append($"<div class=\"uraian\">{Esc(l.Uraian)}</div>");
                sb.Append($"<div class=\"langkah\">{Esc(l.Hitungan)}</div>");
            }
            return sb.ToString();
        }

        private static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
