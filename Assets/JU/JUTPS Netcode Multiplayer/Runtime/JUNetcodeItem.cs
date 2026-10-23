using JUTPS.ItemSystem;
using Unity.Netcode;

namespace JU.TPS.Netcode
{
    /// <summary>
    /// A Netcode synchronized base implementation for <see cref="JUItem"/>.
    /// Use it as a base class for items that need to be synchronized over the network.
    /// </summary>
    /// <typeparam name="T">The type of the item that this component synchronizes.</typeparam>
    public abstract class JUNetcodeItem<
        // The item type that this component synchronizes.
        T
    > : NetworkBehaviour
    where T : JUItem
    {
        private T _item;

        private readonly NetworkVariable<bool> _netUnlocked;
        private readonly NetworkVariable<int> _netQuantity;
        private readonly NetworkVariable<int> _netMaxQuantity;

        /// <summary>
        /// The item attached to this component. Resolved lazily because it is used by <see cref="OnNetworkSpawn"/>, which can run before <see cref="Start"/>.
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

        /// <summary>
        /// Creates the synchronized variables.
        /// </summary>
        protected JUNetcodeItem()
        {
            _netUnlocked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netQuantity = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            _netMaxQuantity = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        }

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
        }

        protected virtual void Update()
        {
            if (IsOwner)
            {
                UpdateIfOwner();
            }
            else
            {
                UpdateIfNotOwner();
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _netUnlocked.OnValueChanged -= UpdateUnlocked;
            _netQuantity.OnValueChanged -= UpdateQuantity;
            _netMaxQuantity.OnValueChanged -= UpdateMaxQuantity;
        }

        /// <summary>
        /// Publishes the state of <see cref="Item"/> to the network. Runs only on the owner.
        /// </summary>
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

        /// <summary>
        /// Extension point for per-frame logic that runs on non-owners.
        /// </summary>
        protected virtual void UpdateIfNotOwner()
        {
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
