using System;
using System.Linq;

namespace PsionClass
{
    internal static class Modals
    {
        // Native YesNo does not call No on X/Escape. Resolve every exit once,
        // after the dialog is gone, so navigation cannot leave orphaned layers.
        internal static Dialog Confirm(string message, Action yes, Action no)
        {
            var game = EClass.game;
            bool accepted = false;
            var dialog = Dialog.YesNo(message, () => accepted = true, () => accepted = false);
            dialog.SetOnKill(() => EClass.core.actionsNextFrame.Add(() => {
                if (EClass.game == game) Plugin.Guard(() => (accepted ? yes : no)?.Invoke());
            }));
            return dialog;
        }

        // Native InputName can invoke its callback both on a button and OnKill.
        internal static Dialog Input(string title, string current, Action<bool, string> completed, Dialog.InputType type)
        {
            var game = EClass.game;
            bool resolved = false, cancelled = true;
            string value = current;
            var backdrop = EClass.ui.layers.LastOrDefault();
            bool hideOnUnfocus = backdrop != null && backdrop.option.hideOnUnfocus;
            Dialog dialog;
            try {
                if (backdrop != null) backdrop.option.hideOnUnfocus = false;
                dialog = Dialog.InputName(title, current, (cancel, text) => {
                    if (resolved) return;
                    resolved = true; cancelled = cancel; value = text;
                }, type);
            }
            finally { if (backdrop != null) backdrop.option.hideOnUnfocus = hideOnUnfocus; }
            dialog.SetOnKill(() => EClass.core.actionsNextFrame.Add(() => {
                if (EClass.game == game) Plugin.Guard(() => completed(cancelled, value));
            }));
            return dialog;
        }
    }
}
