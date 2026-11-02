using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menyalin isi beberapa Text lain ke satu Text, digabung dengan pemisah.
/// Dipakai label kecil di atas bingkai pilihan ATUR KONTROL ("Joystick · 100%"), yang mengambil
/// nama dan ukuran dari toolbar, jadi ControlLayoutEditor tidak perlu diubah.
/// </summary>
[RequireComponent(typeof(Text))]
public sealed class TextMirror : MonoBehaviour {
    [SerializeField] private Text[] sources;
    [SerializeField] private string separator = " · ";

    private Text _target;
    private readonly StringBuilder _builder = new StringBuilder();

    public void Init(Text[] sourceTexts, string join) {
        sources = sourceTexts;
        separator = join;
    }

    private void Awake() => _target = GetComponent<Text>();

    private void LateUpdate() {
        if (_target == null || sources == null)
            return;
        _builder.Clear();
        foreach (Text source in sources) {
            if (source == null || string.IsNullOrEmpty(source.text) || source.text == "-")
                continue;
            if (_builder.Length > 0)
                _builder.Append(separator);
            _builder.Append(source.text);
        }
        string text = _builder.ToString();
        if (_target.text != text)
            _target.text = text;
    }
}
