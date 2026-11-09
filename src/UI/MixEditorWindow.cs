using System.Linq;
using ColossalFramework.UI;
using MixedZoning.Config;
using UnityEngine;

namespace MixedZoning.UI
{
    /// <summary>
    /// In-game, draggable window for editing one zone mix at a time. Built from the game's own
    /// option-screen controls (via UIHelper), so it looks native and blocks clicks to the map.
    /// Opened by right-clicking a mixed-zone button; closed by its X, by Esc, or by
    /// left-clicking any zoning-panel button. The title names the zone being edited.
    /// </summary>
    public class MixEditorWindow : UIPanel
    {
        private const float WindowWidth = 440f;
        private const float Pad = 12f;
        private const float TitleHeight = 42f;

        private static MixEditorWindow instance;

        private UIPanel content;
        private UILabel title;
        private int slot;
        private int pendingSlot = -1;   // rebuilds are deferred: never destroy a control inside its own event

        public static bool IsOpen => instance != null && instance.isVisible;

        public static void Open(int slot)
        {
            if (instance == null)
            {
                instance = (MixEditorWindow)UIView.GetAView().AddUIComponent(typeof(MixEditorWindow));
                instance.Init();
            }
            instance.isVisible = true;
            instance.BringToFront();
            instance.pendingSlot = Mathf.Clamp(slot, 0, MixedZones.Count - 1);
        }

        public static void Close()
        {
            if (instance != null) instance.isVisible = false;
        }

        /// <summary>Close the window if it's showing; returns whether it was.</summary>
        public static bool CloseIfOpen()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        public static void DestroyWindow()
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        private void Init()
        {
            name = "MixedZoningEditor";
            backgroundSprite = "MenuPanel2";
            width = WindowWidth;
            height = 400f;
            isInteractive = true;
            canFocus = true;
            UIView view = GetUIView();
            absolutePosition = new Vector3(Mathf.Round((view.fixedWidth - WindowWidth) / 2f), 140f);

            var drag = AddUIComponent<UIDragHandle>();
            drag.target = this;
            drag.width = WindowWidth;
            drag.height = TitleHeight;
            drag.relativePosition = Vector3.zero;

            title = AddUIComponent<UILabel>();
            title.text = "Mixed Zone";
            title.textScale = 1.1f;
            title.relativePosition = new Vector3(Pad, 13f);

            var close = AddUIComponent<UIButton>();
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.size = new Vector2(32f, 32f);
            close.relativePosition = new Vector3(WindowWidth - 38f, 4f);
            close.eventClick += (c, p) => Close();

            content = AddUIComponent<UIPanel>();
            content.relativePosition = new Vector3(Pad, TitleHeight + 4f);
            content.width = WindowWidth - 2 * Pad;
            content.autoLayout = true;
            content.autoLayoutDirection = LayoutDirection.Vertical;
            content.autoLayoutPadding = new RectOffset(0, 0, 0, 6);
            content.autoFitChildrenVertically = true;
        }

        public override void Update()
        {
            base.Update();
            if (pendingSlot >= 0)
            {
                int next = pendingSlot;
                pendingSlot = -1;
                Rebuild(next);
            }
            float wanted = content.relativePosition.y + content.height + Pad;
            if (!Mathf.Approximately(height, wanted)) height = wanted;
        }

        private void Rebuild(int newSlot)
        {
            slot = newSlot;
            foreach (UIComponent child in content.components.ToArray())
            {
                content.RemoveUIComponent(child);
                Destroy(child.gameObject);
            }

            title.text = MixedZones.DisplayName(slot);

            MixConfig cfg = MixConfig.Current;
            ZoneMix mix = cfg.ForSlot(slot);
            if (mix == null)
            {
                return;
            }
            // Only the six building types; the high-density threshold stays in the options screen.
            Settings.AddMixCheckboxes(new UIHelper(content), mix);
        }
    }
}
