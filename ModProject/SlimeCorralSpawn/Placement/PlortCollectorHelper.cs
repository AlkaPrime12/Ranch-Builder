using System;
using System.Reflection;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Cableado vanilla del Plort Collector (dump Assembly-CSharp v1.2.3):
    /// - PlortCollector: CollectionArea (TrackCollisions), CollectPt, CollectAnim, CollectFX, _vacAudio, _joints
    /// - FixedUpdate mueve plorts hacia CollectPt; DoCollection los elige; StartCollection arranca el ciclo
    /// - PlortCollectorActivator.Collector → Activate() fuerza _forceCollectUntil + animación/FX
    /// </summary>
    internal static class PlortCollectorHelper
    {
        internal static void WireForPlot(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            if (!CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                return;

            CorralRegistrationHelper.EnsurePlotRegion(lp);

            var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
            var pc = CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
            if (pc == null) return;

            WireUpgraderRoot(pcu, pc);
            CorralRegistrationHelper.EnsurePlotRegion(lp);
            EnsureRuntimeRefs(lp, pc);
            WireActivators(lp, pc);
        }

        internal static void EnsurePlotOwnRegion(Il2CppLandPlot lp)
        {
            try { if (lp._region != null) return; } catch { }

            try
            {
                if (lp._region != null) return;
                var prop = typeof(Il2CppLandPlot).GetProperty("_region");
                var comps = lp.GetComponentsInChildren<Component>(true);
                if (comps == null) return;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string tn = null;
                    try { tn = c.GetIl2CppType()?.FullName; } catch { }
                    if (tn != null && tn.EndsWith(".Region", StringComparison.Ordinal))
                    {
                        prop?.SetValue(lp, c);
                        return;
                    }
                }
            }
            catch { }
        }

        internal static void EnsureRuntimeRefs(Il2CppLandPlot lp, Il2Cpp.PlortCollector pc)
        {
            if (pc == null) return;

            try
            {
                if (pc.gameObject != null && !pc.gameObject.activeSelf)
                    pc.gameObject.SetActive(true);
                if (!pc.enabled) pc.enabled = true;
            }
            catch { }

            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc != null && pc._timeDir == null)
                    pc._timeDir = sc.TimeDirector;
            }
            catch { }

            try { if (lp._region != null) pc._region = lp._region; } catch { }

            try
            {
                if (pc.CollectPt == null)
                    pc.CollectPt = FindTransform(pc.transform, "CollectPt", "collect_pt", "VacuumPoint", "Nozzle", "Mouth");
            }
            catch { }

            try
            {
                if (pc.CollectionArea == null)
                    pc.CollectionArea = pc.GetComponentInChildren<Il2Cpp.TrackCollisions>(true);
                // Expandir el collider del área de recolección para que chupe más plorts a la vez
                if (pc.CollectionArea != null)
                {
                    var areaCol = pc.CollectionArea.GetComponent<Collider>();
                    if (areaCol == null) areaCol = pc.CollectionArea.GetComponentInChildren<Collider>(true);
                    if (areaCol != null)
                    {
                        var s = areaCol.transform.localScale;
                        if (s.x < 2f || s.y < 2f || s.z < 2f)
                            areaCol.transform.localScale = new Vector3(Mathf.Max(s.x, 2f), Mathf.Max(s.y, 2f), Mathf.Max(s.z, 2f));
                    }
                }
            }
            catch { }

            try
            {
                if (pc.CollectAnim == null)
                    pc.CollectAnim = FindAnimator(pc.transform, "CollectAnim", "Cyclone", "Vacuum", "Animator");
            }
            catch { }

            try
            {
                if (pc.CollectFX == null)
                {
                    var fx = FindChildGo(pc.transform, "CollectFX", "VacuumFX", "FX", "CycloneFX", "VacFX");
                    if (fx != null) pc.CollectFX = fx;
                }
            }
            catch { }

            try
            {
                if (pc._vacAudio == null)
                {
                    var audios = pc.GetComponentsInChildren<Component>(true);
                    if (audios != null)
                        foreach (var c in audios)
                        {
                            if (c == null) continue;
                            string n = null;
                            try { n = c.GetIl2CppType()?.Name; } catch { }
                            if (n != null && n.Contains("SECTR_AudioSource"))
                            {
                                pc._vacAudio = c.TryCast<Il2Cpp.SECTR_AudioSource>();
                                if (pc._vacAudio != null) break;
                            }
                        }
                }
            }
            catch { }

            try
            {
                var area = pc.CollectionArea;
                if (area != null)
                {
                    if (!area.enabled) area.enabled = true;
                    if (area.gameObject != null && !area.gameObject.activeSelf)
                        area.gameObject.SetActive(true);
                    var col = area.GetComponent<Collider>();
                    if (col != null && !col.enabled) col.enabled = true;
                }
            }
            catch { }
        }

        internal static void WireActivators(Il2CppLandPlot lp, Il2Cpp.PlortCollector pc)
        {
            if (lp == null || pc == null) return;
            try
            {
                var acts = lp.GetComponentsInChildren<Il2Cpp.PlortCollectorActivator>(true);
                if (acts == null) return;
                foreach (var act in acts)
                {
                    if (act == null) continue;
                    WireSingleActivator(act, pc);
                }
            }
            catch { }
        }

        internal static void WireSingleActivator(Il2Cpp.PlortCollectorActivator act, Il2Cpp.PlortCollector pc)
        {
            if (act == null || pc == null) return;
            try { act.Collector = pc; } catch { }
            try
            {
                if (act.gameObject != null && !act.gameObject.activeSelf)
                    act.gameObject.SetActive(true);
            }
            catch { }
            try { if (!act.enabled) act.enabled = true; } catch { }
            try
            {
                if (act._buttonAnimator == null)
                    act._buttonAnimator = act.GetComponentInChildren<Animator>(true);
            }
            catch { }
            InvokeActivatorAwake(act);
        }

        private static readonly System.Collections.Generic.HashSet<int> _activatorAwakeDone = new System.Collections.Generic.HashSet<int>();

        private static void InvokeActivatorAwake(Il2Cpp.PlortCollectorActivator act)
        {
            if (act == null) return;
            int id = act.GetInstanceID();
            if (!_activatorAwakeDone.Add(id)) return;
            try { act.Awake(); } catch { }
        }

        /// <summary>Botón manual: animación, sonido y ventana de recolección forzada.</summary>
        internal static bool TryManualActivate(Il2Cpp.PlortCollectorActivator act, Il2Cpp.PlortCollector pc)
        {
            if (act == null || pc == null) return false;

            var td = pc._timeDir;
            if (td == null)
            {
                try
                {
                    var sc = Il2Cpp.SceneContext.Instance;
                    if (sc != null) td = sc.TimeDirector;
                }
                catch { }
            }
            if (td != null)
            {
                try
                {
                    double now = td.WorldTime();
                    if (now < act._nextAllowedActivationTime) return false;
                    float cooldown = Il2Cpp.PlortCollectorActivator.TIME_BETWEEN_ACTIVATIONS;
                    if (cooldown <= 0f) cooldown = 1f;
                    act._nextAllowedActivationTime = now + cooldown;
                }
                catch { }
            }

            try
            {
                var anim = act._buttonAnimator;
                if (anim != null)
                {
                    if (!anim.enabled) anim.enabled = true;
                    try { anim.SetTrigger(act._buttonPressedTriggerId); } catch { }
                    try { anim.SetTrigger("Pressed"); } catch { }
                }
            }
            catch { }

            try
            {
                var cue = act.PressButtonCue;
                if (cue != null)
                {
                    try
                    {
                        var play = cue.GetType().GetMethod("Play", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        if (play != null && play.GetParameters().Length == 0)
                            play.Invoke(cue, null);
                    }
                    catch { }
                }
            }
            catch { }

            PulseCollection(pc, manual: true);
            return true;
        }

        /// <summary>Mantiene el ciclo FixedUpdate activo sin spamear DoCollection.</summary>
        internal static void MaintainCollection(Il2Cpp.PlortCollector pc)
        {
            if (pc == null) return;
            var td = pc._timeDir;
            if (td == null) return;

            try
            {
                double now = td.WorldTime();
                double window = Il2Cpp.PlortCollector.MAX_COLLECT_TIME > 0f
                    ? Il2Cpp.PlortCollector.MAX_COLLECT_TIME : 4f;
                double until = now + window;
                if (pc._endCollectAt <= now)
                    pc._endCollectAt = until;
            }
            catch { }

            try { pc.StartCollection(); } catch { }
        }

        // Buffer reusable para el OverlapSphere del depósito (sin allocs por tick).
        private static readonly Collider[] _depositOverlap = new Collider[256];   // grande: si hay muchos colliders (terreno/estructuras) los plorts no deben quedar afuera
        private const float ScanRadius = 8f;        // +grande: agarra plorts de todo el corral y más allá
        private const float NozzleRadiusSqr = 1.5f * 1.5f;   // +grande: depósito más tolerante
        private const float SiphonSpeed = 8f;       // +rápido: plorts vuelan más rápido a la boquilla

        /// <summary>
        /// DEPÓSITO + PRESENTACIÓN del aspirador (lógica SEPARADA de la succión).
        /// CLAVE: el depósito agarra los plorts SOLO cuando ya llegaron a la boquilla (radio chico), así NO
        /// se los roba antes de que el ciclo vanilla los chupe con la ANIMACIÓN de la vacaspiradora. Mientras
        /// haya plorts en rango, se fuerza la presentación (ciclón CollectAnim + CollectFX + sonido _vacAudio).
        /// El depósito en sí usa MaybeAddAsResource + Vacuumable.TryConsume.
        /// </summary>
        internal static void ForceDeposit(Il2CppLandPlot lp, Il2Cpp.PlortCollector pc)
        {
            if (pc == null) return;
            Il2Cpp.SiloStorage silo = null;
            try { silo = pc._storage; } catch { }

            // Si el silo está lleno, apagar la aspiradora y no chupar más
            if (silo != null && SiloIsFull(silo))
            {
                int pcidFull = pc.GetInstanceID();
                if (_presenting.Contains(pcidFull))
                {
                    _presenting.Remove(pcidFull);
                    EnableVacuumPresentation(pc, false);
                }
                return;
            }

            Vector3 center;
            try { center = pc.CollectPt != null ? pc.CollectPt.position : pc.transform.position; }
            catch { return; }

            // Escanear AMPLIO desde el cuerpo del collector (cubre corral + boquilla), y filtrar por la ZONA
            // REAL del corral (CollectionArea.bounds) — así NO agarra plorts del rancho ni excluye la boquilla.
            Vector3 scanCenter = center;
            try { scanCenter = pc.transform.position; } catch { }

            Bounds areaBounds = default; bool hasBounds = false;
            try
            {
                var area = pc.CollectionArea;
                if (area != null)
                {
                    var areaCol = area.GetComponent<Collider>();
                    if (areaCol == null) areaCol = area.GetComponentInChildren<Collider>(true);
                    if (areaCol != null) { areaBounds = areaCol.bounds; areaBounds.Expand(2f); hasBounds = true; }
                }
            }
            catch { }

            // USAR Physics.OverlapSphere QUE ALOCA (no NonAlloc): el buffer manejado de NonAlloc dejaba los
            // proxies Il2Cpp INVÁLIDOS (idents=0, col=-). El que aloca los envuelve bien. + incluir TRIGGERS.
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> hits = null;
            try { hits = Physics.OverlapSphere(scanCenter, ScanRadius, ~0, QueryTriggerInteraction.Collide); }
            catch { }
            int n = 0; try { n = hits != null ? hits.Length : 0; } catch { }

            bool anyPlortInRange = false;
            _processedThisCall.Clear();   // dedup por plort en ESTA pasada (un plort tiene varios colliders)

            for (int i = 0; i < n; i++)
            {
                Collider col = null;
                try { col = hits[i]; } catch { }
                if (col == null) continue;

                // Lookup AMPLIO del Identifiable: en el padre, en sí mismo, y en hijos.
                Il2Cpp.Identifiable ident = null;
                try { ident = col.GetComponentInParent<Il2Cpp.Identifiable>(); } catch { }
                if (ident == null) { try { ident = col.GetComponentInChildren<Il2Cpp.Identifiable>(true); } catch { } }
                if (ident == null) continue;

                // DEDUP: un plort tiene varios colliders (físico + trigger) → todos dan el MISMO Identifiable.
                // Procesarlo SOLO UNA VEZ por pasada (si no, se depositaba 2 veces = "1 plort suma 2").
                int identId = 0; try { identId = ident.GetInstanceID(); } catch { }
                if (!_processedThisCall.Add(identId)) continue;

                Il2Cpp.IdentifiableType id = null;
                try { id = ident.identType; } catch { }
                if (id == null) continue;
                if (!LooksLikePlort(id)) continue;   // slimes/comida/otros → ignorar

                Vector3 ppos;
                try { ppos = ident.transform.position; } catch { continue; }

                // FILTRO: solo plorts DENTRO de la zona del corral (no del rancho). Si no hay bounds, cae a radio.
                if (hasBounds) { if (!areaBounds.Contains(ppos)) continue; }
                else { if ((ppos - scanCenter).sqrMagnitude > 4f * 4f) continue; }

                // NO chupar plorts cuyo tipo ya está lleno en el silo (dejarlos para el jugador)
                if (silo != null && CantAcceptMore(silo, id)) continue;

                anyPlortInRange = true;

                // ANTI-DUPLICACIÓN: evitar que el jugador chupe ESTE plort mientras el colector lo
                // está sifoneando. NO destruir el Vacuumable (si el plort rebota fuera del rango,
                // el jugador debe poder agarrarlo). PreventForcesFromWeaponVacuum frena la aspiradora
                // del jugador pero NO impide agarrar manualmente si el plort sale del rango.
                Il2CppMonomiPark.SlimeRancher.Vacuumable vac0 = null;
                try { vac0 = ident.GetComponent<Il2CppMonomiPark.SlimeRancher.Vacuumable>(); } catch { }
                if (vac0 == null) { try { vac0 = ident.GetComponentInParent<Il2CppMonomiPark.SlimeRancher.Vacuumable>(); } catch { } }
                if (vac0 != null) { try { vac0.PreventForcesFromWeaponVacuum = true; } catch { } }

                float distSqr = (ppos - center).sqrMagnitude;

                // SIFÓN: si el plort todavía NO llegó a la boquilla, jalarlo hacia ella (chupar adentro).
                // Gravedad OFF para que no caiga entre ticks + velocidad hacia la boquilla = vuela al aspirador.
                if (distSqr > NozzleRadiusSqr)
                {
                    try
                    {
                        var rb = ident.GetComponent<Rigidbody>();
                        if (rb == null) rb = ident.GetComponentInParent<Rigidbody>();
                        if (rb != null)
                        {
                            try { rb.useGravity = false; } catch { }
                            try { if (rb.IsSleeping()) rb.WakeUp(); } catch { }
                            Vector3 dir = center - rb.position;
                            float d = dir.magnitude;
                            if (d > 0.001f) rb.velocity = (dir / d) * SiphonSpeed;
                        }
                    }
                    catch { }
                    continue;   // todavía no depositar
                }

                // LLEGÓ a la boquilla → meter en silo + destruir el GameObject.
                // NO llamar TryConsume (eso le da el plort al inventario del jugador = duplicación).
                if (silo == null || !DepositToSilo(silo, id)) continue;
                try { UnityEngine.Object.Destroy(ident.gameObject); } catch { }
            }

            // PRESENTACIÓN: ciclón + FX + sonido ON sólo si hay PLORTS REALES en rango; OFF cuando no hay.
            // MaintainCollection se mantiene (mueve la animación vanilla) — NO duplica el depósito porque el
            // depósito lo hacemos sólo nosotros (con dedup), el ciclo vanilla en custom no deposita.
            int pcid = pc.GetInstanceID();
            bool wasPresenting = _presenting.Contains(pcid);
            if (anyPlortInRange && !wasPresenting)
            {
                _presenting.Add(pcid);
                EnableVacuumPresentation(pc, true);
                try { MaintainCollection(pc); } catch { }
            }
            else if (!anyPlortInRange && wasPresenting)
            {
                _presenting.Remove(pcid);
                EnableVacuumPresentation(pc, false);
            }
        }
        private static readonly System.Collections.Generic.HashSet<int> _processedThisCall = new System.Collections.Generic.HashSet<int>();

        private static readonly System.Collections.Generic.HashSet<int> _presenting = new System.Collections.Generic.HashSet<int>();

        /// <summary>Heurística: el identType es un plort (para decidir si mostrar la animación del aspirador).</summary>
        private static bool LooksLikePlort(Il2Cpp.IdentifiableType id)
        {
            try { return id != null && id.name != null && id.name.IndexOf("plort", StringComparison.OrdinalIgnoreCase) >= 0; }
            catch { return false; }
        }

        /// <summary>
        /// Busca si el tipo 'id' YA ocupa un slot. Retorna el índice del slot, o -1 si no está en ninguno.
        /// </summary>
        private static int FindSlotForType(Il2Cpp.SiloStorage silo, Il2Cpp.IdentifiableType id, int slots)
        {
            for (int i = 0; i < slots; i++)
            {
                try
                {
                    var sid = silo.GetSlotIdentifiable(i);
                    if (sid != null && sid.name == id.name) return i;
                }
                catch { }
            }
            return -1;
        }

        /// <summary>
        /// Retorna true si el silo NO puede aceptar MÁS plorts del tipo 'id'.
        /// Si el tipo ya está en un slot y ese slot está lleno → true.
        /// Si el tipo no está en ningún slot y no hay slots vacíos → true (silo lleno de otros tipos).
        /// </summary>
        private static bool CantAcceptMore(Il2Cpp.SiloStorage silo, Il2Cpp.IdentifiableType id)
        {
            int slots = 0;
            try { slots = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { }
            if (slots <= 0) slots = 2;

            int existing = FindSlotForType(silo, id, slots);
            if (existing >= 0)
            {
                try
                {
                    int count = silo.GetSlotCount(existing);
                    if (count >= MaxSlotCount) return true;
                }
                catch { }
                return false;
            }

            for (int i = 0; i < slots; i++)
            {
                try
                {
                    var sid = silo.GetSlotIdentifiable(i);
                    if (sid == null) return false;
                }
                catch { }
            }
            return true;
        }

        /// <summary>Retorna true si TODOS los slots están llenos (apagar presentación).</summary>
        private static bool SiloIsFull(Il2Cpp.SiloStorage silo)
        {
            int slots = 0;
            try { slots = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { }
            if (slots <= 0) slots = 2;
            for (int i = 0; i < slots; i++)
            {
                try
                {
                    var id = silo.GetSlotIdentifiable(i);
                    int count = silo.GetSlotCount(i);
                    if (id == null || count < MaxSlotCount) return false;
                }
                catch { }
            }
            return true;
        }

        private const int MaxSlotCount = 100;

        /// <summary>
        /// Mete 1 unidad de 'id' en el silo. Si el tipo ya está en un slot → apila ahí.
        /// Si ese slot está lleno → rechaza. Si el tipo no está en ningún slot → primer slot VACÍO.
        /// NUNCA mete un tipo en otro slot que ya tiene un tipo distinto (no se desborda).
        /// </summary>
        private static bool DepositToSilo(Il2Cpp.SiloStorage silo, Il2Cpp.IdentifiableType id)
        {
            int slots = 0;
            try { slots = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { }
            if (slots <= 0) slots = 2;

            int existing = FindSlotForType(silo, id, slots);
            if (existing >= 0)
            {
                try { if (silo.MaybeAddAsResource(id, existing, 1, false)) return true; } catch { }
                return false;
            }

            for (int i = 0; i < slots; i++)
            {
                try
                {
                    var sid = silo.GetSlotIdentifiable(i);
                    if (sid == null)
                        if (silo.MaybeAddAsResource(id, i, 1, false)) return true;
                }
                catch { }
            }
            return false;
        }

        internal static void WireUpgraderRoot(Il2Cpp.PlortCollectorUpgrader pcu, Il2Cpp.PlortCollector pc)
        {
            if (pcu == null || pc == null) return;
            try
            {
                var root = pc.transform.parent != null ? pc.transform.parent.gameObject : pc.gameObject;
                if (pcu.Collector == null)
                    pcu.Collector = root;
                else if (!pcu.Collector.activeSelf)
                    pcu.Collector.SetActive(true);
            }
            catch { }
        }

        /// <summary>Impulso de recolección (manual o auto). Activa ventana FixedUpdate + DoCollection.</summary>
        internal static void PulseCollection(Il2Cpp.PlortCollector pc, bool manual)
        {
            if (pc == null) return;

            var td = pc._timeDir;
            if (td != null)
            {
                try
                {
                    double now = td.WorldTime();
                    double window = Il2Cpp.PlortCollector.MAX_COLLECT_TIME > 0f
                        ? Il2Cpp.PlortCollector.MAX_COLLECT_TIME : 3f;
                    double until = now + window;
                    if (manual)
                        pc._forceCollectUntil = until;
                    if (pc._endCollectAt <= now)
                        pc._endCollectAt = until;
                }
                catch { }
            }

            EnableVacuumPresentation(pc, true);

            try { pc.StartCollection(); } catch { }
            try { pc.DoCollection(); } catch { }
        }

        internal static void EnableVacuumPresentation(Il2Cpp.PlortCollector pc, bool on)
        {
            if (pc == null) return;

            // Partículas del aspirador (CollectFX): ON/OFF.
            try
            {
                if (pc.CollectFX != null && pc.CollectFX.activeSelf != on)
                    pc.CollectFX.SetActive(on);
            }
            catch { }

            // Animación del ciclón (CollectAnim): setear el bool del animator ON/OFF.
            try
            {
                var anim = pc.CollectAnim;
                if (anim != null)
                {
                    if (on && !anim.enabled) anim.enabled = true;
                    try { anim.SetBool(pc._animCycloneActiveId, on); } catch { }
                    if (on)
                    {
                        try { anim.SetTrigger("CycloneActive"); } catch { }
                        try { anim.SetTrigger("Active"); } catch { }
                    }
                }
            }
            catch { }

            // Sonido de succión (_vacAudio): Play al encender, Stop al apagar.
            try
            {
                var audio = pc._vacAudio;
                if (audio != null)
                {
                    if (on)
                    {
                        if (!audio.enabled) audio.enabled = true;
                        try { audio.Play(); } catch { }
                    }
                    else
                    {
                        try { audio.Stop(false); } catch { }
                    }
                }
            }
            catch { }
        }

        private static Transform FindTransform(Transform root, params string[] names)
        {
            if (root == null || names == null) return null;
            foreach (var n in names)
            {
                var t = FindDeep(root, n);
                if (t != null) return t;
            }
            return null;
        }

        private static Animator FindAnimator(Transform root, params string[] names)
        {
            var t = FindTransform(root, names);
            if (t != null)
            {
                try { var a = t.GetComponent<Animator>(); if (a != null) return a; } catch { }
            }
            try { return root.GetComponentInChildren<Animator>(true); } catch { return null; }
        }

        private static GameObject FindChildGo(Transform root, params string[] names)
        {
            var t = FindTransform(root, names);
            return t != null ? t.gameObject : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
