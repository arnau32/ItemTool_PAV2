using UnityEngine;

[CreateAssetMenu(fileName = "Open Shop", menuName = "Dialogue/Actions/Open Shop", order = 3)]
public class OpenShopAction : DialogueAction
{
    public override void Execute()
    {
        // TODO: wire to ShopService when implemented.
        // Example: GameServices.Get<ShopService>().OpenShop();
        Debug.Log("[OpenShopAction] TODO: wire to ShopService.");
    }
}