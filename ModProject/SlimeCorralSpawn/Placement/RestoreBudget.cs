namespace SlimeCorralSpawn.Placement
{
    /// <summary>Presupuesto de respawn al cargar partida (sin bajar calidad). Bajo a propósito: cada LandPlot
    /// real es un clon PESADO; instanciar pocos por frame reparte el trabajo del arranque → muy poco lag al
    /// entrar (tarda unos frames más en aparecer todo, pero cada frame es liviano).</summary>
    public static class RestoreBudget
    {
        public const int PlotsPerFrame = 2;        // plots reales = caros → de a poco
        public const int StructuresPerFrame = 4;   // estructuras = más livianas
    }
}
