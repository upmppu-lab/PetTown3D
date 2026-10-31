using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 왼쪽 위 프로필 카드(마을 이름·내 이름)와 이름 짓기 창.
public sealed partial class PetTownCozyHudView
{
    RectTransform profilePanel, peopleChip;
    TMP_InputField playerInput, townInput;
    TextMeshProUGUI townLabel, nameLabel;

    TextMeshProUGUI HudBoldText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var label = theme.Text(name, parent, text, size, color, align);
        label.fontStyle = FontStyles.Bold; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        var bold = theme.Font.fontWeightTable[7].regularTypeface; if (bold) label.font = bold;
        return label;
    }

    void BuildProfileTexts()
    {
        townLabel = HudBoldText("TownName", profile, "", 17, Color.white, TextAlignmentOptions.Left);
        Fixed(townLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -6), new Vector2(160, 24));
        LabelOutline(townLabel); townLabel.fontSharedMaterial.SetFloat("_OutlineWidth", .24f);
        nameLabel = HudBoldText("PlayerName", profile, "", 16, new Color(1f, .93f, .7f), TextAlignmentOptions.Left);
        Fixed(nameLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(96, -58), new Vector2(160, 26));
        LabelOutline(nameLabel); nameLabel.fontSharedMaterial.SetFloat("_OutlineWidth", .24f);
    }

    void BuildPopulationChip(Action<int> navigate)
    {
        peopleChip = Chip("Population", layout, theme.Style.hudChip);
        var image = peopleChip.GetComponent<Image>();
        var button = peopleChip.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = UnityEngine.UI.Selectable.Transition.None; button.onClick.AddListener(() => navigate(6));
        var icon = PetTownUITheme.Rect("PeopleIcon", peopleChip).gameObject.AddComponent<Image>();
        icon.sprite = PetTownGlyphs.Glyph(PetTownGlyph.People); icon.preserveAspect = true; icon.raycastTarget = false;
        Fixed(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, 0), new Vector2(20, 20));
        population = HudBoldText("PopulationValue", peopleChip, "", 14, Color.white, TextAlignmentOptions.Center);
        PetTownUITheme.Place(population.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-6, 0));
        population.enableAutoSizing = true; population.fontSizeMin = 10; population.fontSizeMax = 14;
    }

    TMP_InputField MakeInput(Transform parent, string placeholder, Vector2 position)
    {
        var root = PetTownUITheme.Rect("Input", parent); var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(1, 1, .96f, .96f);
        Fixed(root, new Vector2(.5f, 1), new Vector2(.5f, 1), position, new Vector2(280, 38));
        var area = PetTownUITheme.Rect("TextArea", root); PetTownUITheme.Place(area, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4)); area.gameObject.AddComponent<RectMask2D>();
        var text = theme.Text("Text", area, "", 17, new Color(.16f, .1f, .05f), TextAlignmentOptions.Left); PetTownUITheme.Place(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); text.textWrappingMode = TextWrappingModes.NoWrap;
        var hint = theme.Text("Placeholder", area, placeholder, 17, new Color(.5f, .45f, .4f), TextAlignmentOptions.Left); PetTownUITheme.Place(hint.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); hint.textWrappingMode = TextWrappingModes.NoWrap;
        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area; input.textComponent = text; input.placeholder = hint; input.characterLimit = 12; input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    void ProfileButton(Transform parent, string text, Color color, Vector2 position, Vector2 size, Action action)
    {
        var root = PetTownUITheme.Rect("Button_" + text, parent); var image = root.gameObject.AddComponent<Image>(); image.sprite = PetTownGlyphs.Plate(); image.type = Image.Type.Sliced; image.color = color;
        var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
        Fixed(root, new Vector2(.5f, 0), new Vector2(.5f, 0), position, size);
        var label = HudBoldText("Label", root, text, 15, Color.white, TextAlignmentOptions.Center); PetTownUITheme.Place(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    void BuildProfilePanel(Action<int> navigate)
    {
        profilePanel = PetTownUITheme.Rect("ProfileDim", layout); PetTownUITheme.Place(profilePanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dim = profilePanel.gameObject.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .5f);
        profilePanel.gameObject.AddComponent<Button>().onClick.AddListener(() => profilePanel.gameObject.SetActive(false));
        var card = PetTownUITheme.Rect("Card", profilePanel); var cardImage = card.gameObject.AddComponent<Image>(); cardImage.sprite = PetTownGlyphs.Plate(); cardImage.type = Image.Type.Sliced; cardImage.color = new Color32(0x2A, 0x24, 0x22, 245);
        card.gameObject.AddComponent<Button>().transition = UnityEngine.UI.Selectable.Transition.None;
        Fixed(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(330, 330));
        var title = HudBoldText("Title", card, "내 정보", 20, Color.white, TextAlignmentOptions.Center); Fixed(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(300, 30));
        var l1 = HudBoldText("Label1", card, "내 이름", 14, new Color(1f, .93f, .7f), TextAlignmentOptions.Left); Fixed(l1.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(280, 22));
        playerInput = MakeInput(card, "내 이름 (최대 12자)", new Vector2(0, -80));
        var l2 = HudBoldText("Label2", card, "마을 이름", 14, new Color(1f, .93f, .7f), TextAlignmentOptions.Left); Fixed(l2.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -130), new Vector2(280, 22));
        townInput = MakeInput(card, "마을 이름 (최대 12자)", new Vector2(0, -154));
        ProfileButton(card, "저장", Hex(0x4C, 0xAF, 0x6A), new Vector2(0, 98), new Vector2(280, 40), () =>
        {
            var game = PetTownGame.Instance; if (game != null) game.SetPlayerTownNames(playerInput.text, townInput.text);
            profilePanel.gameObject.SetActive(false);
        });
        ProfileButton(card, "캐릭터 꾸미기", Hex(0x8E, 0x7A, 0xE0), new Vector2(-72, 50), new Vector2(132, 38), () => { profilePanel.gameObject.SetActive(false); navigate(3); });
        ProfileButton(card, "닫기", Hex(0x6C, 0x7A, 0x89), new Vector2(72, 50), new Vector2(132, 38), () => profilePanel.gameObject.SetActive(false));
        profilePanel.gameObject.SetActive(false);
        BuildWeatherIcon();
    }

    void OpenProfilePanel()
    {
        if (!profilePanel) return;
        var game = PetTownGame.Instance;
        if (game != null) { playerInput.text = game.PlayerName; townInput.text = game.TownName; }
        profilePanel.SetAsLastSibling(); profilePanel.gameObject.SetActive(true);
    }
}
