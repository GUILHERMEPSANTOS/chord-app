"""Converte os rótulos dos modelos para os acordes aceitos pelo domínio .NET."""

NOTES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
ENHARMONIC = {"Db": "C#", "Eb": "D#", "Gb": "F#", "Ab": "G#", "Bb": "A#", "Cb": "B", "B#": "C", "Fb": "E", "E#": "F"}


def simplify(label: str) -> str:
    """Reduz uma cifra Harte às classes maiores, menores, sétimas e diminutas do MVP."""
    if label == "N":
        return "N"
    root, _, quality = label.partition(":")
    root = ENHARMONIC.get(root, root)
    if root not in NOTES:
        return "N"
    quality = quality.split("/", 1)[0]
    if not quality:
        return root
    if quality == "hdim7":
        return root + "m7b5"
    if quality == "dim7":
        return root + "dim7"
    if quality == "dim":
        return root + "dim"
    if quality == "maj7":
        return root + "maj7"
    if quality == "min7":
        return root + "m7"
    if quality == "7":
        return root + "7"
    if quality.startswith("min"):
        return root + "m"
    if quality.startswith("maj"):
        return root
    return "N"
