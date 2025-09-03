using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Voodoo.Tiny.Sauce.Internal.Analytics
{
     [Serializable]
    public class ItemTransactionParameters
    {
        /// <summary>
        /// Information about the item that is being transacted.
        /// Mandatory
        /// </summary>
        public ItemTransactionInfo item;

        /// <summary>
        /// Number of units spent or received.
        /// Should be positive for both transaction types.
        /// Mandatory
        /// </summary>
        public float nbUnits;
        
        /// <summary>
        /// "In" if the item is received, "Out" if the item is used or spent.
        /// Mandatory
        /// </summary>
        public TransactionType transactionType;

        /// <summary>
        /// Level at which the transaction occurred (either pre-level, in-game or end-of-level).
        /// Can be NULL if not applicable.
        /// Level should always be stored here, not in sub_placement.
        /// </summary>
        [CanBeNull]
        public string level;
        
        /// <summary>
        /// Cost of one unit. Can be 0 if the item is given for free.
        /// </summary>
        [CanBeNull]
        public float? unitCost;

        /// <summary>
        /// Example: ‘coins’ or ‘iap’.
        /// Must be ‘iap’ if done through an iap.
        /// Must be a game-specific enum.
        /// </summary>
        [CanBeNull]
        public Enum currencyUsed;
        
        /// <summary>
        /// Balance after the transaction was realised.
        /// Can be null.
        /// </summary>
        [CanBeNull]
        public float? balance = null;
        
        /// <summary>
        /// If currency_used is 'iap', then we store the currency used for the purchase (e.g. "eur", "usd")
        /// It should be 'null' if the currency_used was not 'iap'.
        /// </summary>
        [CanBeNull]
        public string iapLocalCurrency;
        
        /// <summary>
        /// Where did the transaction originate from?
        /// Example: "market", "free_chest"
        /// Must be a game-specific enum.
        /// </summary>
        [CanBeNull]
        public Enum placement;
        
        /// <summary>
        /// If you need more granularity regarding placement. E.g.
        /// placement="rv"
        /// sub_placement="end_of_level_multiplier"
        /// </summary>
        [CanBeNull]
        public string subPlacement;
        
        /// <summary>
        /// Id of the placement if we keep track of it. E.g. the RV id if we receive the items from watching an RV.
        /// If an IAP, use the "transaction_id" if available
        /// </summary>
        [CanBeNull]
        public string placementId;
        
        /// <summary>
        /// Additional custom parameters.
        /// </summary>
        [CanBeNull]
        public Dictionary<string, object> eventContextProperties = new Dictionary<string, object>();

        public ItemTransactionParameters(ItemTransactionInfo item, int nbUnits, TransactionType transactionType)
        {
            this.item = item;
            this.nbUnits = nbUnits;
            this.transactionType = transactionType;
        }
    }

    [Serializable]
    public struct ItemTransactionInfo
    {
        /// <summary>
        /// Example: "wood" or "coins"
        /// Must be a game specific enum.
        /// </summary>
        public Enum itemName;
        
        /// <summary>
        /// Direct type of the item.
        /// </summary>
        public ItemType itemType;

        public ItemTransactionInfo(Enum name, ItemType type)
        {
            itemName = name;
            itemType = type;
        }
    }

    public enum TransactionType
    {
        In, Out,
    }

    public enum ItemType
    {
        soft_currency,
        hard_currency,
        consumable,
        persistent,
        other, 
        unknown,
    }
}