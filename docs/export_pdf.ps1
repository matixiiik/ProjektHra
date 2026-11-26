# ─────────────────────────────────────────────────────────────────────────────
#  export_pdf.ps1 — převede .docx z této složky na PDF přes Microsoft Word.
#  Word při tom aktualizuje obsah (TOC) a čísla stránek, takže PDF je hotové
#  k odevzdání (škola chce dokumentaci v PDF).
#
#  Použití (PowerShell ve složce docs):
#      .\export_pdf.ps1                                   # všechny dokumenty
#      .\export_pdf.ps1 Dokumentace_The_Last_Beacon.docx  # jen jeden
# ─────────────────────────────────────────────────────────────────────────────
param([string[]]$Files = @('Dokumentace_The_Last_Beacon.docx', 'Uzivatelska_prirucka_The_Last_Beacon.docx'))

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    foreach ($f in $Files) {
        $src = Join-Path $PSScriptRoot $f
        if (-not (Test-Path $src)) { Write-Warning "Nenalezeno: $src"; continue }
        $pdf = [IO.Path]::ChangeExtension($src, '.pdf')

        $doc = $word.Documents.Open($src, $false, $false)
        foreach ($toc in $doc.TablesOfContents) { $toc.Update() }  # obsah podle aktuálních nadpisů
        $doc.Fields.Update() | Out-Null                            # čísla stránek a ostatní pole
        $doc.Save()
        $doc.ExportAsFixedFormat($pdf, 17)                         # 17 = wdExportFormatPDF
        $doc.Close($false)
        Write-Output "PDF: $pdf"
    }
}
finally {
    $word.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
}
