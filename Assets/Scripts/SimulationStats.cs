using UnityEngine;

public class SimulationStats : MonoBehaviour
{
    public static SimulationStats Instance;

    [Header("Test Settings")]
    [SerializeField] private int targetCompletedCustomers = 20;

    private int completedCustomers = 0;

    private float totalCompletionTime = 0f;
    private float totalShelfWaitTime = 0f;
    private float totalQueueWaitTime = 0f;

    private int totalItemsBought = 0;
    private int totalItemsSkipped = 0;
    private int totalItemSwitches = 0;

    private bool testFinished = false;
    private string finalReport = "";

    private void Awake()
    {
        Instance = this;
    }

    public void RecordCustomer(
        float completionTime,
        float shelfWaitTime,
        float queueWaitTime,
        int itemsBought,
        int itemsSkipped,
        int itemSwitches)
    {
        if (testFinished)
            return;

        completedCustomers++;

        totalCompletionTime += completionTime;
        totalShelfWaitTime += shelfWaitTime;
        totalQueueWaitTime += queueWaitTime;

        totalItemsBought += itemsBought;
        totalItemsSkipped += itemsSkipped;
        totalItemSwitches += itemSwitches;

        if (completedCustomers >= targetCompletedCustomers)
            FinishTest();
    }

    private void FinishTest()
    {
        testFinished = true;
        finalReport = BuildReport();

        Debug.Log(finalReport);
    }

    private string BuildReport()
    {
        if (completedCustomers == 0)
            return "No customers completed.";

        return
            "TEST FINISHED\n" +
            "Completed customers: " + completedCustomers + "\n" +
            "Average completion time: " + (totalCompletionTime / completedCustomers).ToString("0.00") + "s\n" +
            "Average shelf wait time: " + (totalShelfWaitTime / completedCustomers).ToString("0.00") + "s\n" +
            "Average queue wait time: " + (totalQueueWaitTime / completedCustomers).ToString("0.00") + "s\n" +
            "Average items bought: " + ((float)totalItemsBought / completedCustomers).ToString("0.00") + "\n" +
            "Average skipped items: " + ((float)totalItemsSkipped / completedCustomers).ToString("0.00") + "\n" +
            "Average item switches: " + ((float)totalItemSwitches / completedCustomers).ToString("0.00");
    }

    public string GetReport()
    {
        if (testFinished)
            return finalReport;

        return
            "Running test...\n" +
            "Completed customers: " + completedCustomers + " / " + targetCompletedCustomers;
    }

    public bool IsTestFinished()
    {
        return testFinished;
    }
}