Simple Language Processor
=========================

_Deutsch:_

Dieses simple Werkzeug wurde in C# geschrieben und kann sämtliche Wörter aus
nicht-verkürzenden grammatischen Regeln erzeugen.

Dafür muss eine Datei erstellt werden, die alle Regeln und Terminale enthält und
optional eine maximale Länge (Standard: 5).

Die Datei sollte so ähnlich wie hier aussehen:

```
# Maximale Länge
n=5

# Terminale
L={a,b}

# Regeln
S->AB
A->Aa|a
B->Bb|b
```

Die Wörter können dann mittels ``langproc <Datei>`` berechnet werden.

Bitte benutzt es nur zur Überprüfung der Funktionalität von grammatischen Regeln.

_English:_

This simple tool is written in C# and is able to calculate possible words from
non-shortening grammatic rules.

For this to work, create a file containing the grammatic rules and terminals and
optionally variables defining the maximum length (defaults to 5).

The file should look similar to this:

```
# Maximum length
n=5

# Terminals
L={a,b}

# Grammatic rules
S->AB
A->Aa|a
B->Bb|b
```

The words can then be calculated via ``langproc <Datei>``.

Please use it only to make sure your grammatic rules would work out properly.

License
-------

This work is licensed under the GNU General Public License Version 3.
For more info look into the LICENSE.txt file.