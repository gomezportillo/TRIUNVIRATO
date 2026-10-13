using UnityEngine;

/// <summary>Displays army balances and purchase controls using the prototype's existing IMGUI approach.</summary>
public sealed class EconomyUI : MonoBehaviour
{
    [SerializeField] private ArmyEconomy economy;
    [SerializeField] private TowerShop shop;

    private static readonly ArmyType[] Armies = { ArmyType.Tierra, ArmyType.Aire, ArmyType.Mar };
    private static readonly Color[] Colors =
    {
        new Color(0.25f, 0.8f, 0.32f),
        new Color(1f, 0.25f, 0.22f),
        new Color(0.2f, 0.55f, 1f)
    };
    private static readonly string[] Names = { "Tierra", "Aire", "Mar" };

    private string feedback;
    private float feedbackTime;

    private void OnGUI()
    {
        if (economy == null || shop == null)
            return;

        GUILayout.BeginArea(new Rect(18f, 18f, 580f, 155f), GUI.skin.box);
        GUILayout.Label("RECURSOS DEL EJÉRCITO");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < Armies.Length; i++)
        {
            GUI.contentColor = Colors[i];
            GUILayout.Label("● " + Names[i], GUILayout.Width(90f));
            GUI.contentColor = Color.white;
            GUILayout.Label(economy.GetWholeCoins(Armies[i]) + "  (1/s)", GUILayout.Width(90f));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Armies.Length; i++)
        {
            ArmyType army = Armies[i];
            if (GUILayout.Button(shop.GetButtonLabel(army), GUILayout.Height(64f)))
            {
                bool wasOwned = shop.HasTower(army);
                if (!shop.TryPurchase(army, out string failureReason))
                {
                    feedback = failureReason;
                    feedbackTime = 2f;
                }
                else
                {
                    feedback = wasOwned
                        ? "Torre de " + Names[i] + " mejorada: +1% daño."
                        : "Torre de " + Names[i] + " comprada.";
                    feedbackTime = 1.5f;
                }
            }
        }
        GUILayout.EndHorizontal();

        if (feedbackTime > 0f)
        {
            feedbackTime -= Time.unscaledDeltaTime;
            GUILayout.Label(feedback);
        }
        GUILayout.EndArea();
    }
}
