using Unity.Netcode;

public class RuntimeParent : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        transform.SetParent(RuntimeRoot.Instance.transform, true);
    }
}
