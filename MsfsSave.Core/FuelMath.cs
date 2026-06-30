namespace MsfsSave.Core;

public static class FuelMath
{
    public static double ClampToCapacity(double requestedGallons, double capacityGallons)
    {
        if (requestedGallons < 0) return 0;
        if (requestedGallons > capacityGallons) return capacityGallons;
        return requestedGallons;
    }
}
