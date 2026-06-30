#!/usr/bin/env python3
"""Generate ItemSlot prefab and outfit panels in OutfitScene."""

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PREFAB_DIR = ROOT / "Assets/OutGame/Prefabs"
SCENES = [
    ROOT / "Assets/Scenes/OutfitScene.unity",
]

ITEM_SLOT_GUID = "c4e8a1b23d5f6479a8b2c1d0e9f3a7b6"
CANVAS_RT = 1459093583
MANAGER_COMPONENT = 44498979

GUIDS = {
    "Image": "fe87c0e1cc204ed48ad3b37840f39efc",
    "Button": "4e29b1a8efbd4b44bb3f3716e73f07ff",
    "TMP": "f4688fdb7df04437aeb418b961361dc5",
    "ScrollRect": "1aa08ab6e0800fa44ae55d278d1423e3",
    "GridLayoutGroup": "8a8695521f0d02e499659fee002a26c2",
    "ContentSizeFitter": "3245ec927659c4140ac4f8d17403cc18",
    "RectMask2D": "3312d7739989d2b4e91e6319e9a96d76",
    "Outline": "e19747de3f5aca642ab2be37e372fb86",
    "ItemSlotView": "8669b8ce91636493a9467798436e35cc",
    "OutfitItemPanelController": "6bc1034faae2e41c8bbe126cff366b99",
    "TMP_Font": "8f586378b4e144a9851e7b34d9b748ee",
    "UISprite": "0000000000000000f000000000000000",
}

PANELS = [
    ("ItemTopPanel", 0, "Tops", 290001000),
    ("ItemBottomPanel", 1, "Bottoms", 290002000),
    ("ItemWeaponPanel", 2, "Weapons", 290003000),
]


def script_ref(guid: str) -> str:
    return f"{{fileID: 11500000, guid: {guid}, type: 3}}"


def prefab_ref(guid: str) -> str:
    return f"{{fileID: 11400000, guid: {guid}, type: 3}}"


def file_ref(fid: int) -> str:
    return f"{{fileID: {fid}}}"


def generate_item_slot_prefab() -> str:
    g = 291000000
    go, rt, cr, img, outline, btn, slot = g, g + 1, g + 2, g + 3, g + 4, g + 5, g + 6
    icon_go, icon_rt, icon_cr, icon_img = g + 10, g + 11, g + 12, g + 13
    label_go, label_rt, label_cr, label_tmp = g + 20, g + 21, g + 22, g + 23

    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt}}}
  - component: {{fileID: {cr}}}
  - component: {{fileID: {img}}}
  - component: {{fileID: {outline}}}
  - component: {{fileID: {btn}}}
  - component: {{fileID: {slot}}}
  m_Layer: 5
  m_Name: ItemSlot
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {icon_rt}}}
  - {{fileID: {label_rt}}}
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 120, y: 120}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.55, g: 0.55, b: 0.55, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {file_ref(GUIDS['UISprite'])}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &{outline}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Outline'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Outline
  m_EffectColor: {{r: 1, g: 0.85, b: 0.2, a: 1}}
  m_EffectDistance: {{x: 3, y: -3}}
  m_UseGraphicAlpha: 1
--- !u!114 &{btn}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Button'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Button
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {file_ref(img)}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{slot}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['ItemSlotView'])}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::ItemSlotView
  backgroundImage: {file_ref(img)}
  iconImage: {file_ref(icon_img)}
  labelText: {file_ref(label_tmp)}
  selectionOutline: {file_ref(outline)}
  button: {file_ref(btn)}
--- !u!1 &{icon_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {icon_rt}}}
  - component: {{fileID: {icon_cr}}}
  - component: {{fileID: {icon_img}}}
  m_Layer: 5
  m_Name: Icon
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{icon_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {icon_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -20, y: -20}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{icon_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {icon_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{icon_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {icon_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &{label_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {label_rt}}}
  - component: {{fileID: {label_cr}}}
  - component: {{fileID: {label_tmp}}}
  m_Layer: 5
  m_Name: Label
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{label_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{label_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{label_tmp}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {label_go}}}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['TMP'])}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: 
  m_isRightToLeft: 0
  m_fontAsset: {prefab_ref(GUIDS['TMP_Font'])}
  m_sharedMaterial: {{fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4294967295
  m_fontColor: {{r: 1, g: 1, b: 1, a: 1}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: 0
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 18
  m_fontSizeBase: 18
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 1
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 0
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
"""


def generate_panel(base: int, name: str, item_type: int, title: str) -> tuple[str, int, int]:
    """Returns YAML block and panel root RectTransform id."""
    ids = {k: base + i for i, k in enumerate([
        "go", "rt", "cr", "dim", "ctrl",
        "frame_go", "frame_rt", "frame_cr", "frame_img",
        "header_go", "header_rt",
        "title_go", "title_rt", "title_cr", "title_tmp",
        "close_go", "close_rt", "close_cr", "close_img", "close_btn",
        "close_txt_go", "close_txt_rt", "close_txt_cr", "close_txt_tmp",
        "scroll_go", "scroll_rt", "scroll_comp",
        "viewport_go", "viewport_rt", "viewport_cr", "viewport_img", "viewport_mask",
        "content_go", "content_rt", "grid", "fitter",
    ])}

    # unpack
    go, rt = ids["go"], ids["rt"]
    cr, dim, ctrl = ids["cr"], ids["dim"], ids["ctrl"]
    frame_go, frame_rt, frame_cr, frame_img = ids["frame_go"], ids["frame_rt"], ids["frame_cr"], ids["frame_img"]
    header_go, header_rt = ids["header_go"], ids["header_rt"]
    title_go, title_rt, title_cr, title_tmp = ids["title_go"], ids["title_rt"], ids["title_cr"], ids["title_tmp"]
    close_go, close_rt, close_cr, close_img, close_btn = ids["close_go"], ids["close_rt"], ids["close_cr"], ids["close_img"], ids["close_btn"]
    close_txt_go, close_txt_rt, close_txt_cr, close_txt_tmp = ids["close_txt_go"], ids["close_txt_rt"], ids["close_txt_cr"], ids["close_txt_tmp"]
    scroll_go, scroll_rt, scroll_comp = ids["scroll_go"], ids["scroll_rt"], ids["scroll_comp"]
    viewport_go, viewport_rt, viewport_cr, viewport_img, viewport_mask = ids["viewport_go"], ids["viewport_rt"], ids["viewport_cr"], ids["viewport_img"], ids["viewport_mask"]
    content_go, content_rt, grid, fitter = ids["content_go"], ids["content_rt"], ids["grid"], ids["fitter"]

    yaml = f"""
--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt}}}
  - component: {{fileID: {cr}}}
  - component: {{fileID: {dim}}}
  - component: {{fileID: {ctrl}}}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!224 &{rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {frame_rt}}}
  m_Father: {{fileID: {CANVAS_RT}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{dim}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0, g: 0, b: 0, a: 0.65}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &{ctrl}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['OutfitItemPanelController'])}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::OutfitItemPanelController
  filterType: {item_type}
  contentRoot: {file_ref(content_rt)}
  scrollRect: {file_ref(scroll_comp)}
  closeButton: {file_ref(close_btn)}
  slotPrefab: {prefab_ref(ITEM_SLOT_GUID)}
"""
    # Frame, header, scroll hierarchy - abbreviated generation continues in same function
    yaml += _frame_and_scroll_yaml(ids, title)
    return yaml, go, rt


def _frame_and_scroll_yaml(ids: dict, title: str) -> str:
    frame_go, frame_rt, frame_cr, frame_img = ids["frame_go"], ids["frame_rt"], ids["frame_cr"], ids["frame_img"]
    header_go, header_rt = ids["header_go"], ids["header_rt"]
    title_go, title_rt, title_cr, title_tmp = ids["title_go"], ids["title_rt"], ids["title_cr"], ids["title_tmp"]
    close_go, close_rt, close_cr, close_img, close_btn = ids["close_go"], ids["close_rt"], ids["close_cr"], ids["close_img"], ids["close_btn"]
    close_txt_go, close_txt_rt, close_txt_cr, close_txt_tmp = ids["close_txt_go"], ids["close_txt_rt"], ids["close_txt_cr"], ids["close_txt_tmp"]
    scroll_go, scroll_rt, scroll_comp = ids["scroll_go"], ids["scroll_rt"], ids["scroll_comp"]
    viewport_go, viewport_rt, viewport_cr, viewport_img, viewport_mask = ids["viewport_go"], ids["viewport_rt"], ids["viewport_cr"], ids["viewport_img"], ids["viewport_mask"]
    content_go, content_rt, grid, fitter = ids["content_go"], ids["content_rt"], ids["grid"], ids["fitter"]
    go, rt = ids["go"], ids["rt"]

    return f"""--- !u!1 &{frame_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {frame_rt}}}
  - component: {{fileID: {frame_cr}}}
  - component: {{fileID: {frame_img}}}
  m_Layer: 5
  m_Name: PanelFrame
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{frame_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {frame_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {header_rt}}}
  - {{fileID: {scroll_rt}}}
  m_Father: {{fileID: {rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.08, y: 0.12}}
  m_AnchorMax: {{x: 0.92, y: 0.88}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{frame_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {frame_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{frame_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {frame_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.95, g: 0.95, b: 0.95, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {file_ref(GUIDS['UISprite'])}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!1 &{header_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {header_rt}}}
  m_Layer: 5
  m_Name: Header
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{header_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {header_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {title_rt}}}
  - {{fileID: {close_rt}}}
  m_Father: {{fileID: {frame_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 72}}
  m_Pivot: {{x: 0.5, y: 1}}
--- !u!1 &{title_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {title_rt}}}
  - component: {{fileID: {title_cr}}}
  - component: {{fileID: {title_tmp}}}
  m_Layer: 5
  m_Name: Title
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{title_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {title_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {header_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -144, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{title_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {title_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{title_tmp}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {title_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['TMP'])}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0, g: 0, b: 0, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: {title}
  m_isRightToLeft: 0
  m_fontAsset: {prefab_ref(GUIDS['TMP_Font'])}
  m_sharedMaterial: {{fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4278190080
  m_fontColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: 0
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 36
  m_fontSizeBase: 36
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 1
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 1
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 0
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 24, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
--- !u!1 &{close_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {close_rt}}}
  - component: {{fileID: {close_cr}}}
  - component: {{fileID: {close_img}}}
  - component: {{fileID: {close_btn}}}
  m_Layer: 5
  m_Name: CloseButton
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{close_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {close_txt_rt}}}
  m_Father: {{fileID: {header_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 1, y: 0.5}}
  m_AnchorMax: {{x: 1, y: 0.5}}
  m_AnchoredPosition: {{x: -12, y: 0}}
  m_SizeDelta: {{x: 72, y: 56}}
  m_Pivot: {{x: 1, y: 0.5}}
--- !u!222 &{close_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{close_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0.85, g: 0.85, b: 0.85, a: 1}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {file_ref(GUIDS['UISprite'])}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &{close_btn}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Button'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Button
  m_Navigation:
    m_Mode: 3
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_PressedColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}}
    m_SelectedColor: {{r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}}
    m_DisabledColor: {{r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {file_ref(close_img)}
  m_OnClick:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{close_txt_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {close_txt_rt}}}
  - component: {{fileID: {close_txt_cr}}}
  - component: {{fileID: {close_txt_tmp}}}
  m_Layer: 5
  m_Name: Text
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{close_txt_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_txt_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {close_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{close_txt_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_txt_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{close_txt_tmp}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {close_txt_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['TMP'])}
  m_Name: 
  m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI
  m_Material: {{fileID: 0}}
  m_Color: {{r: 0, g: 0, b: 0, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: X
  m_isRightToLeft: 0
  m_fontAsset: {prefab_ref(GUIDS['TMP_Font'])}
  m_sharedMaterial: {{fileID: 2180264, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4278190080
  m_fontColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: 0
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 28
  m_fontSizeBase: 28
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 1
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 0
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
--- !u!1 &{scroll_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {scroll_rt}}}
  - component: {{fileID: {scroll_comp}}}
  m_Layer: 5
  m_Name: ScrollView
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{scroll_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {scroll_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {viewport_rt}}}
  m_Father: {{fileID: {frame_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: -32, y: -104}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!114 &{scroll_comp}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {scroll_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['ScrollRect'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.ScrollRect
  m_Content: {file_ref(content_rt)}
  m_Horizontal: 0
  m_Vertical: 1
  m_MovementType: 2
  m_Elasticity: 0.1
  m_Inertia: 1
  m_DecelerationRate: 0.135
  m_ScrollSensitivity: 30
  m_Viewport: {file_ref(viewport_rt)}
  m_HorizontalScrollbar: {{fileID: 0}}
  m_VerticalScrollbar: {{fileID: 0}}
  m_HorizontalScrollbarVisibility: 0
  m_VerticalScrollbarVisibility: 0
  m_HorizontalScrollbarSpacing: 0
  m_VerticalScrollbarSpacing: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
--- !u!1 &{viewport_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {viewport_rt}}}
  - component: {{fileID: {viewport_cr}}}
  - component: {{fileID: {viewport_img}}}
  - component: {{fileID: {viewport_mask}}}
  m_Layer: 5
  m_Name: Viewport
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{viewport_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {viewport_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {content_rt}}}
  m_Father: {{fileID: {scroll_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!222 &{viewport_cr}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {viewport_go}}}
  m_CullTransparentMesh: 1
--- !u!114 &{viewport_img}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {viewport_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['Image'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 0.01}}
  m_RaycastTarget: 1
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 0}}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &{viewport_mask}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {viewport_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['RectMask2D'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.RectMask2D
  m_Padding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Softness: {{x: 0, y: 0}}
--- !u!1 &{content_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {content_rt}}}
  - component: {{fileID: {grid}}}
  - component: {{fileID: {fitter}}}
  m_Layer: 5
  m_Name: Content
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{content_rt}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {content_go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {viewport_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 1}}
--- !u!114 &{grid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {content_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['GridLayoutGroup'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.GridLayoutGroup
  m_Padding:
    m_Left: 8
    m_Right: 8
    m_Top: 8
    m_Bottom: 8
  m_ChildAlignment: 0
  m_StartCorner: 0
  m_StartAxis: 0
  m_CellSize: {{x: 120, y: 120}}
  m_Spacing: {{x: 12, y: 12}}
  m_Constraint: 1
  m_ConstraintCount: 4
--- !u!114 &{fitter}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {content_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {script_ref(GUIDS['ContentSizeFitter'])}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.ContentSizeFitter
  m_HorizontalFit: 0
  m_VerticalFit: 2
"""


def patch_scene(scene_path: Path) -> None:
    text = scene_path.read_text(encoding="utf-8")

    # Remove previously generated panels if re-running
    text = re.sub(r"\n--- !u!1 &29000\d{4}[\s\S]*?(?=\n--- !u!1660057539)", "", text)

    panel_gos = []
    panel_rts = []
    blocks = []
    for name, item_type, title, base in PANELS:
        block, go, rt = generate_panel(base, name, item_type, title)
        blocks.append(block)
        panel_gos.append(go)
        panel_rts.append(rt)

    # Update canvas children
    canvas_children_match = re.search(
        r"(--- !u!224 &1459093583\nRectTransform:[\s\S]*?m_Children:\n)(  - [\s\S]*?)(  m_Father:)",
        text,
    )
    if not canvas_children_match:
        raise RuntimeError("Canvas RectTransform not found")
    existing_children = canvas_children_match.group(2)
    new_children = existing_children.rstrip() + "\n"
    for rt in panel_rts:
        new_children += f"  - {{fileID: {rt}}}\n"
    text = text[: canvas_children_match.start(2)] + new_children + text[canvas_children_match.start(3) :]

    # Wire OutfitSceneManager
    text = re.sub(
        r"(--- !u!114 &44498979\nMonoBehaviour:[\s\S]*?itemTopPanel: )\{fileID: 0\}",
        rf"\1{{fileID: {panel_gos[0]}}}",
        text,
        count=1,
    )
    text = re.sub(
        r"(itemBottomPanel: )\{fileID: 0\}",
        rf"\1{{fileID: {panel_gos[1]}}}",
        text,
        count=1,
    )
    text = re.sub(
        r"(itemWeaponPanel: )\{fileID: 0\}",
        rf"\1{{fileID: {panel_gos[2]}}}",
        text,
        count=1,
    )

    # Insert panel YAML before SceneRoots
    insert_point = text.rfind("--- !u!1660057539 &9223372036854775807")
    text = text[:insert_point] + "".join(blocks) + "\n" + text[insert_point:]

    scene_path.write_text(text, encoding="utf-8")
    print(f"Patched {scene_path}")


def main() -> None:
    PREFAB_DIR.mkdir(parents=True, exist_ok=True)
    prefab_path = PREFAB_DIR / "ItemSlot.prefab"
    prefab_path.write_text(generate_item_slot_prefab(), encoding="utf-8")
    meta_path = PREFAB_DIR / "ItemSlot.prefab.meta"
    if not meta_path.exists():
        meta_path.write_text(
            f"fileFormatVersion: 2\nguid: {ITEM_SLOT_GUID}\nPrefabImporter:\n"
            "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
            encoding="utf-8",
        )
    for scene in SCENES:
        patch_scene(scene)
    print("Done.")


if __name__ == "__main__":
    main()
