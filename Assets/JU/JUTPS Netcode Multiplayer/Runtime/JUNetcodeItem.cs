using JUTPS.ItemSystem;
using Unity.Netcode;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// A Netcode synchronized base implementation for <see cref="JUItem"/>.
    /// Use it as a base class for items that need to be synchronized over the network.
    /// </summary>
    /// <typeparam name="T">The type of the item that this component synchronizes.</typeparam>
    public abstract class JUNetcodeItem<T> : NetworkBehaviour where T : JUItem
    {
        private T _item;

        private NetworkVariable<bool> _netUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netQuantity = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _netMaxQuantity = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// The item attached to this component.
        /// </summary>
        public T Item
        {
            get
            {
                if (_item == null)
                {
                    _item = GetComponent<T>();
                }

                return _item;
            }
        }

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
        }

        protected virtual void Update()
        {
            UpdateIfOwner();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                return;
            }

            _netUnlocked.OnValueChanged += UpdateUnlocked;
            _netQuantity.OnValueChanged += UpdateQuantity;
            _netMaxQuantity.OnValueChanged += UpdateMaxQuantity;

            UpdateUnlocked(_netUnlocked.Value, _netUnlocked.Value);
            UpdateQuantity(_netQuantity.Value, _netQuantity.Value);
            UpdateMaxQuantity(_netMaxQuantity.Value, _netMaxQuantity.Value);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _netUnlocked.OnValueChanged -= UpdateUnlocked;
            _netQuantity.OnValueChanged -= UpdateQuantity;
            _netMaxQuantity.OnValueChanged -= UpdateMaxQuantity;
        }

        protected virtual void UpdateIfOwner()
        {
            if (IsOwner == false)
            {
                return;
            }

            _netUnlocked.Value = Item.Unlocked;
            _netQuantity.Value = Item.ItemQuantity;
            _netMaxQuantity.Value = Item.MaxItemQuantity;
        }

        private void UpdateUnlocked(bool previous, bool current)
        {
            Item.Unlocked = current;
        }

        private void UpdateQuantity(int previous, int current)
        {
            Item.ItemQuantity = current;
        }

        private void UpdateMaxQuantity(int previous, int current)
        {
            Item.MaxItemQuantity = current;
        }
    }
}