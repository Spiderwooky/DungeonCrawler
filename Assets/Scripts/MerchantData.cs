using System;
using UnityEngine;

/// <summary>
/// Définition d'une marchande : identité, personnalité (injectée dans le prompt système
/// de Claude), marges de négociation et articles en vente. Sert de template en lecture
/// seule — l'état mutable (humeur, appréciation, stock restant) vit dans le composant
/// Merchant à l'exécution, jamais écrit ici.
/// Créer via : clic droit > Create > NPC > Merchant Data
/// </summary>
[CreateAssetMenu(fileName = "NewMerchantData", menuName = "NPC/Merchant Data")]
public class MerchantData : ScriptableObject
{
    [Header("Identité")]
    public string merchantName = "Marchande";

    [TextArea(4, 10)]
    [Tooltip("Décrit son caractère, son ton, ses manies de langage et ce qu'elle valorise. Injecté dans le prompt système envoyé à Claude (mode IA uniquement).")]
    public string personality = "";

    [Header("Humeur et appréciation (mode IA)")]
    [Tooltip("Humeur de départ : -10 (agacée) à +10 (ravie).")]
    [Range(-10, 10)] public int startingMood = 0;

    [Tooltip("Appréciation de départ envers le joueur : 0 à 100.")]
    [Range(0, 100)] public int startingApproval = 50;

    [Tooltip("Influence de l'appréciation sur ses marges de négociation, en % d'écart maximum par rapport au prix de référence (à 50 d'appréciation, aucun effet).")]
    [Range(0, 30)] public int approvalPriceInfluencePercent = 10;

    [Header("Marges de négociation par défaut (% de la valeur de base de l'objet)")]
    [Tooltip("Prix de vente qu'elle demande par défaut au joueur.")]
    [Min(0)] public int sellAskPercent = 120;

    [Tooltip("Prix de vente plancher qu'elle acceptera en négociant.")]
    [Min(0)] public int sellMinPercent = 90;

    [Tooltip("Prix d'achat qu'elle propose par défaut pour les objets du joueur.")]
    [Min(0)] public int buyOfferPercent = 50;

    [Tooltip("Prix d'achat plafond qu'elle acceptera en négociant.")]
    [Min(0)] public int buyMaxPercent = 70;

    [Header("Articles en vente")]
    public MerchantStockEntry[] stock = Array.Empty<MerchantStockEntry>();

    [Header("Mode hors-ligne (clé API absente ou réseau indisponible)")]
    [TextArea(2, 4)] public string fallbackGreeting = "Bonjour, regarde mes articles si quelque chose te plaît.";
    [TextArea(2, 4)] public string fallbackPurchaseSuccess = "Voilà, merci de ton achat.";
    [TextArea(2, 4)] public string fallbackPurchaseFailure = "Tu n'as pas assez d'or, ou je n'en ai plus en stock.";
    [TextArea(2, 4)] public string fallbackSaleSuccess = "Marché conclu, merci.";
    [TextArea(2, 4)] public string fallbackSaleFailure = "Je n'achète pas ça, ou tu n'en as pas assez.";
    [TextArea(2, 4)] public string fallbackFarewell = "Au revoir, reviens quand tu veux.";
}

/// <summary>Un article proposé par la marchande, avec son stock de départ (template).</summary>
[Serializable]
public class MerchantStockEntry
{
    public ItemData item;

    [Tooltip("0 = utiliser un prix calculé depuis la valeur de base de l'objet et les marges de la marchande.")]
    [Min(0)] public int priceOverride = 0;

    [Tooltip("Stock de départ. -1 = illimité.")]
    public int stock = -1;
}
