using System.Collections.Generic;
using UnityEngine;

public partial class CustomerAgent
{
    // optiune posibila pentru utility ai
    private class UtilityOption
    {
        public Decision Decision;
        public float Score;
        public string Reason;
        public GridCell ChosenShelf;
        public GridCell ChosenSpot;
        public int OtherItemIndex = -1;
    }


    // alege actiunea cu scorul cel mai mare
    private Decision DecideNextActionUtility(out ProductType wanted, out GridCell chosenShelf, out GridCell chosenSpot)
    {
        wanted = ProductType.None;
        chosenShelf = null;
        chosenSpot = null;

        if (!HasFinishedShopping())
        {
            if (currentItemIndex < 0 || currentItemIndex >= shoppingList.Count)
                currentItemIndex = 0;
            //verifica ce produs vrea clientul acum
            wanted = shoppingList[currentItemIndex];
        }

        //construieste lista de optiuni posibile
        List<UtilityOption> options = BuildUtilityOptions(wanted);
        UtilityOption best = null;

        // gaseste optiunea cu scorul cel mai mare
        foreach (UtilityOption option in options)
        {
            if (best == null || option.Score > best.Score)
                best = option;
        }

        if (best == null)
        {
            note = "Utility AI found no option";
            return Decision.WaitForShelf;
        }

        // aplica efectele deciziei alese
        ApplyUtilitySideEffects(best);

        chosenShelf = best.ChosenShelf;
        chosenSpot = best.ChosenSpot;
        note = best.Reason + " | Score: " + best.Score.ToString("0.00");

        //returneaza decizia finala
        return best.Decision;
    }


    // construieste lista de optiuni posibile pentru agent
    private List<UtilityOption> BuildUtilityOptions(ProductType wanted)
    {
        List<UtilityOption> options = new List<UtilityOption>();

        AddGoToQueueUtility(options);
        AddSkipItemUtility(options, wanted);
        AddGoToShelfUtility(options, wanted);
        AddTryOtherItemUtility(options);
        AddWaitForShelfUtility(options, wanted);

        return options;
    }

    // calculeaza scorul pentru mersul la coada
    private void AddGoToQueueUtility(List<UtilityOption> options)
    {
        float score = HasFinishedShopping() ? 1f : 0f;

        options.Add(new UtilityOption
        {
            Decision = Decision.GoToQueue,
            Score = score,
            Reason = "Utility: finished shopping, go to queue"
        });
    }


    // adauga optiunea de skip daca produsul nu exista in magazin
    private void AddSkipItemUtility(List<UtilityOption> options, ProductType wanted)
    {
        if (HasFinishedShopping())
            return;

        bool productMissing = grid.FindShelves(wanted).Count == 0;
        float score = productMissing ? 0.95f : 0f;

        options.Add(new UtilityOption
        {
            Decision = Decision.SkipItem,
            Score = score,
            Reason = "Utility: product missing, skip item"
        });
    }


    // calculeaza scorul pentru mersul la cel mai bun raft disponibil
    private void AddGoToShelfUtility(List<UtilityOption> options, ProductType wanted)
    {
        if (HasFinishedShopping())
            return;

        // cauta rafturile care au produsul dorit
        List<ShelfOption> shelfOptions = GetShelfOptions(wanted, lenient: true);

        if (shelfOptions.Count == 0)
            return;

        //alege cel mai bun raft disponibil
        ShelfOption bestShelf = GetClosestOption(shelfOptions, onlyNear: false);

        if (bestShelf == null)
            return;

        //calculeaza scorul pe baza distantei si a numarului de incercari esuate de a ajunge la raft
        float distanceScore = GetDistanceUtility(bestShelf.Distance);
        float attemptPenalty = Mathf.Clamp01(shelfBlockedAttempts / 8f) * 0.3f;
        float score = Mathf.Clamp01(0.55f * distanceScore + 0.45f - attemptPenalty);

        options.Add(new UtilityOption
        {
            Decision = Decision.GoToShelf,
            Score = score,
            ChosenShelf = bestShelf.Shelf,
            ChosenSpot = bestShelf.Spot,
            Reason = $"Utility: shelf available, dist:{distanceScore:0.00} attempts:{shelfBlockedAttempts}"
        });
    }


    // calculeaza scorul pentru schimbarea produsului curent
    private void AddTryOtherItemUtility(List<UtilityOption> options)
    {
        if (HasFinishedShopping())
            return;

        if (itemSwitchCooldown > 0f)
            return;

        // cauta un alt produs disponibil in lista de cumparaturi
        int otherItemIndex = FindAvailableItemIndex();

        if (otherItemIndex == -1)
            return;

        //calculeaza scorul pe baza numarului de incercari esuate de a ajunge la raft pentru produsul curent
        float impatience = Mathf.Clamp01(shelfBlockedAttempts / 8f);
        float score = 0.1f + 0.65f * impatience;

        options.Add(new UtilityOption
        {
            Decision = Decision.TryOtherItem,
            Score = score,
            OtherItemIndex = otherItemIndex,
            Reason = $"Utility: other item available, attempts:{shelfBlockedAttempts} impatience:{impatience:0.00}"
        });
    }


    // calculeaza scorul pentru asteptarea unui raft liber
    private void AddWaitForShelfUtility(List<UtilityOption> options, ProductType wanted)
    {
        if (HasFinishedShopping())
            return;

        bool productExists = grid.FindShelves(wanted).Count > 0;

        if (!productExists)
            return;

        //verifica daca exista rafturi disponibile pentru produsul dorit
        bool hasShelfOptions = GetShelfOptions(wanted, lenient: true).Count > 0;
        
        //calculeaza rabdarea 
        float patience = 1f - Mathf.Clamp01(shelfBlockedAttempts / 8f);

        //daca exista rafturi disponibile, asteptarea are scor mic
        float score = hasShelfOptions ? 0.05f : 0.55f * patience;

        options.Add(new UtilityOption
        {
            Decision = Decision.WaitForShelf,
            Score = score,
            Reason = $"Utility: patience:{patience:0.00} attempts:{shelfBlockedAttempts}"
        });
    }

    // transforma distanta pana la raft intr-un scor intre 0 si 1
    private float GetDistanceUtility(int distance)
    {
        if (distance <= 0)
            return 1f;

        float normalizedDistance = Mathf.Clamp01((float)distance / nearShelfDistance);
        return 1f - normalizedDistance;
    }


    // aplica efectele deciziei alese, cum ar fi resetarea timerelor sau schimbarea itemului
    private void ApplyUtilitySideEffects(UtilityOption option)
    {
        switch (option.Decision)
        {
            case Decision.GoToQueue:
                shelfWaitTimer = 0f;
                shelfBlockedAttempts = 0;
                break;

            case Decision.GoToShelf:
                shelfWaitTimer = 0f;
                break;

           
            case Decision.TryOtherItem:
                if (option.OtherItemIndex != -1)
                {
                    currentItemIndex = option.OtherItemIndex; // schimba produsul curent cu altul disponibil
                    shelfWaitTimer = 0f;
                    shelfBlockedAttempts = 0;
                    itemSwitchCooldown = maxShelfWaitTime * 0.5f; //asteapta 2 secunde pana mai incearca alt item
                    itemSwitches++;
                }
                break;
        }
    }
}