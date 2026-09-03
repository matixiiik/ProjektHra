#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
#  compile-check.sh
#  Zkontroluje, jestli se herní kód (Assembly-CSharp) přeloží — BEZ nutnosti
#  otevírat Unity. Používá Roslyn kompilátor zabalený v Unity a přesně ty
#  parametry, které Unity používá při normální kompilaci (Bee response file).
#
#  Výstup si nikam netrvale neukládá (jde do scratch složky), takže NEROZBIJE
#  Unity build cache.
#
#  Použití:
#     bash .claude/skills/unity-hra/scripts/compile-check.sh
#
#  Návratový kód 0 = přeloží se, != 0 = chyby (vypíšou se).
# ─────────────────────────────────────────────────────────────────────────────
set -u

# Kořen projektu = dva adresáře nad tímto skriptem (.claude/skills/unity-hra/scripts).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
cd "$PROJECT_ROOT" || { echo "❌ Nenašel jsem kořen projektu"; exit 2; }

# 1) Verze Unity editoru z ProjectVersion.txt.
VER="$(grep -m1 '^m_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}' | tr -d '\r')"
[ -z "$VER" ] && { echo "❌ Nezjistil jsem verzi Unity"; exit 2; }
EDITOR="/c/Program Files/Unity/Hub/Editor/$VER/Editor/Data"
[ -d "$EDITOR" ] || EDITOR="C:/Program Files/Unity/Hub/Editor/$VER/Editor/Data"
[ -d "$EDITOR" ] || { echo "❌ Nenašel jsem Unity $VER v C:/Program Files/Unity/Hub/Editor"; exit 2; }

DOTNET="$EDITOR/NetCoreRuntime/dotnet.exe"
CSC="$EDITOR/DotNetSdkRoslyn/csc.dll"
[ -f "$DOTNET" ] || { echo "❌ Chybí $DOTNET"; exit 2; }
[ -f "$CSC" ]    || { echo "❌ Chybí $CSC"; exit 2; }

# 2) Bee response file (parametry kompilace). Bereme ne-Dbg variantu.
RSP="$(ls -t Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp 2>/dev/null | grep -v 'Dbg.dag' | head -1)"
[ -z "$RSP" ] && RSP="$(ls -t Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp 2>/dev/null | head -1)"
[ -z "$RSP" ] && { echo "❌ Nenašel jsem Assembly-CSharp.rsp (otevři projekt v Unity aspoň jednou, ať se vygeneruje)"; exit 2; }

# 3) Kopie rsp s přesměrovaným výstupem. Výstup jde do Temp/ (gitignored,
#    Unity si ho nehlídá) a cesta je RELATIVNÍ ke kořeni projektu — csc.exe
#    běží s cwd = kořen projektu, takže se cesty nerozjedou mezi Git Bash a Windows.
OUT_DIR="Temp/unity-hra-compile"
mkdir -p "$PROJECT_ROOT/$OUT_DIR"
CHECK_RSP="$PROJECT_ROOT/$OUT_DIR/check.rsp"
sed -e 's#-out:"[^"]*"#-out:"'"$OUT_DIR"'/check.dll"#' \
    -e 's#-refout:"[^"]*"#-nowarn:CS0414#' \
    -e '/additionalfile/d' \
    "$RSP" > "$CHECK_RSP"

echo "🔨 Kompiluji Assembly-CSharp (Unity $VER)…"
OUTPUT="$("$DOTNET" "$CSC" "@$CHECK_RSP" 2>&1)"
CODE=$?

# Vyfiltruj skutečné chyby/varování (ignoruj info hlášky a potlačené warningy).
ERRORS="$(printf '%s\n' "$OUTPUT" | grep -E ': error [A-Z]+[0-9]+:' )"
WARNINGS="$(printf '%s\n' "$OUTPUT" | grep -E ': warning [A-Z]+[0-9]+:' | grep -vE 'CS0169|CS0649|CS0414')"

if [ -n "$ERRORS" ]; then
    echo ""
    echo "❌ CHYBY:"
    printf '%s\n' "$ERRORS"
    exit 1
fi

if [ -n "$WARNINGS" ]; then
    echo ""
    echo "⚠️  Varování (kód se přeloží, ale mrkni na to):"
    printf '%s\n' "$WARNINGS"
fi

if [ $CODE -eq 0 ]; then
    echo "✅ Kód se přeloží bez chyb."
    exit 0
else
    echo ""
    echo "❌ Kompilátor skončil s kódem $CODE:"
    printf '%s\n' "$OUTPUT" | tail -20
    exit 1
fi
