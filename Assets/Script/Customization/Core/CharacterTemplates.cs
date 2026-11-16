using System.Collections.Generic;

/// <summary>Satu template karakter: id CharacterDatabase + CharacterObject BoZo asalnya.</summary>
public readonly struct CharacterTemplate {
    /// <summary>Id di CharacterDatabase / PlayerProgress.characterId (jangan diganti setelah rilis).</summary>
    public readonly string Id;
    /// <summary>Nama CharacterObject BoZo di Resources (CustomCharacters/Resources).</summary>
    public readonly string BozoId;
    public readonly string Label;
    public readonly bool IsMale;
    public readonly bool IsAnime;

    public CharacterTemplate(string id, string bozoId, string label, bool isMale, bool isAnime) {
        Id = id;
        BozoId = bozoId;
        Label = label;
        IsMale = isMale;
        IsAnime = isAnime;
    }

    public string GenderLabel => IsMale ? "Cowok" : "Cewek";
    public string StyleLabel => IsAnime ? "Anime" : "Default";
}

/// <summary>4 template yang bisa dipilih dan menjadi dasar karakter custom.</summary>
public static class CharacterTemplates {
    public static readonly IReadOnlyList<CharacterTemplate> All = new[] {
        new CharacterTemplate("male", "1DefaultMale", "Default Cowok", isMale: true, isAnime: false),
        new CharacterTemplate("female", "1DefaultFemale", "Default Cewek", isMale: false, isAnime: false),
        new CharacterTemplate("anime_male", "0_DefaultAnimeMale", "Anime Cowok", isMale: true, isAnime: true),
        new CharacterTemplate("anime_female", "0_DefaultAnimeFemale", "Anime Cewek", isMale: false, isAnime: true)
    };

    public static bool TryGet(string id, out CharacterTemplate template) {
        foreach (CharacterTemplate candidate in All) {
            if (candidate.Id != id) continue;
            template = candidate;
            return true;
        }
        template = default;
        return false;
    }
}
