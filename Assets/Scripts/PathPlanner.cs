using System.Collections.Generic;
using UnityEngine;

public enum PlanAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    Pick, Drop
}

public enum Heading
{
    Up,
    Right,
    Down,
    Left
}

public enum DeliveryMode
{
    None,
    NearestTarget,   // A
    FixedPriority,   // B
    IdMapping        // C
}

public class PathPlanner : MonoBehaviour
{
    [Header("References")]
    public static PathPlanner Instance;
    public GridGenerator uiGrid;
    public UdpRobotSender udpSender;
    public ArduinoSerialSender serialSender;

    [Header("Runtime Feedback (for UI highlighting)")]
    [Tooltip("Índice del segmento activo (0..N-1). -1 = ninguno.")]
    public int ActiveSegmentIndex { get; private set; } = -1;

    [Tooltip("Número total de segmentos (R→B→T) del plan actual.")]
    public int TotalSegments { get; private set; } = 0;

    /// Se dispara cuando cambia el segmento activo. Pasa -1 para 'ninguno'.
    public event System.Action<int> OnActiveSegmentChanged;

    /// Llama a esto cuando el robot empieza a ejecutar el plan (p.ej. al enviar el plan).
    /// Llama a esto cuando el robot empieza a ejecutar el plan (p.ej. al enviar el plan).
    public void NotifyPlanStarted()
    {
        // Calcula cuántos segmentos hay para que el UI pueda saber cuándo terminar
        var segs = BuildPathSegments();
        TotalSegments = (segs != null) ? segs.Count : 0;

        // Empieza resaltando el segmento 0 si hay alguno
        ActiveSegmentIndex = (TotalSegments > 0) ? 0 : -1;
        OnActiveSegmentChanged?.Invoke(ActiveSegmentIndex);
    }   



    /// Llama a esto cuando el robot termina un segmento (normalmente justo después de un DROP).
    public void NotifySegmentCompleted()
    {
        if (TotalSegments <= 0) return;

        if (ActiveSegmentIndex < 0) ActiveSegmentIndex = 0;
        else ActiveSegmentIndex++;

        if (ActiveSegmentIndex >= TotalSegments)
            ActiveSegmentIndex = -1; // plan terminado

        OnActiveSegmentChanged?.Invoke(ActiveSegmentIndex);
    }

    /// Llama a esto si el robot se detiene/aborta/finaliza y quieres quitar el highlight.
    public void NotifyPlanStopped()
    {
        ActiveSegmentIndex = -1;
        OnActiveSegmentChanged?.Invoke(ActiveSegmentIndex);
    }



    private void Awake()
    {
        Instance = this;
    }

    [Header("Delivery Mode (A/B/C)")]
    public DeliveryMode deliveryMode = DeliveryMode.NearestTarget;

    // =====================
    // ID MAPPING (MODO C)
    // =====================
    // BoxID -> TargetID (ambos son "order": 0-based)
    private readonly Dictionary<int, int> _idMapping = new Dictionary<int, int>();

    public void ClearIdMappings()
    {
        _idMapping.Clear();
    }

    public void SetIdMapping(int boxOrder, int targetOrder)
    {
        _idMapping[boxOrder] = targetOrder;
        Debug.Log($"[PathPlanner] Mapping guardado: B{boxOrder + 1} -> Target {targetOrder + 1}");
    }

    // ✅ NUEVO: cargar todos los mappings de golpe (desde IdMappingUIManager)
    public void SetIdMappings(Dictionary<int, int> mapping)
    {
        _idMapping.Clear();

        if (mapping == null) return;

        foreach (var kv in mapping)
            _idMapping[kv.Key] = kv.Value;

        Debug.Log($"[PathPlanner] SetIdMappings: {_idMapping.Count} mappings cargados.");
    }

    // ✅ NUEVO: helper que pone modo C, guarda mapping y computa (para el botón principal)
    public void ComputePathWithIdMapping(Dictionary<int, int> mapping)
    {
        Debug.Log($"[PathPlanner] ComputePathWithIdMapping called. incomingCount={(mapping==null ? -1 : mapping.Count)}");

        deliveryMode = DeliveryMode.IdMapping;
        SetIdMappings(mapping);

        Debug.Log($"[PathPlanner] deliveryMode={deliveryMode}. computing...");
        ComputePathButton();
    }


    // Botón Compute Path
    public void ComputePathButton()
    {
        var plan = BuildPlan();
        if (plan == null) return;

        var robotCommands = PlanToRobotCommands(plan);
        NotifyPlanStarted(); // ✅ arranca el highlight en el segmento 0


        Debug.Log("=== ROBOT COMMANDS ===");
        Debug.Log(string.Join(" ", robotCommands));

        if (udpSender != null) udpSender.SendPlan(robotCommands);
        else if (serialSender != null) serialSender.SendPlan(robotCommands);
        else Debug.LogWarning("PathPlanner: no hay udpSender ni serialSender asignado. Solo se imprime el plan.");
    }

    public List<PlanAction> BuildPlan()
    {
        switch (deliveryMode)
        {
            case DeliveryMode.NearestTarget:
                Debug.Log(">>> MODO A: NearestTarget");
                return BuildPlan_NearestTarget();

            case DeliveryMode.FixedPriority:
                Debug.Log(">>> MODO B: FixedPriority");
                return BuildPlan_FixedPriority();

            case DeliveryMode.IdMapping:
                Debug.Log(">>> MODO C: IdMapping");
                return BuildPlan_IdMapping();

            default:
                Debug.LogError("PathPlanner: deliveryMode = None/invalid.");
                return null;
        }
    }

    // -------------------------
    // Leer grid: robot, boxes, targets (targets ordenados por targetOrder)
    // -------------------------
    private bool ReadGrid(
        out CellPaintMode[,] state,
        out Vector2Int start,
        out List<Vector2Int> boxes,
        out List<Vector2Int> targetsByPriority,
        out int rows,
        out int cols)
    {
        state = null;
        start = default;
        boxes = new List<Vector2Int>();
        targetsByPriority = new List<Vector2Int>();
        rows = cols = 0;

        if (uiGrid == null)
        {
            Debug.LogError("PathPlanner: uiGrid no asignado.");
            return false;
        }

        rows = uiGrid.Rows;
        cols = uiGrid.Columns;

        if (rows <= 0 || cols <= 0)
        {
            Debug.LogError("PathPlanner: rows/cols inválidos. Genera el grid primero.");
            return false;
        }

        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();
        if (cells.Length != rows * cols)
        {
            Debug.LogError($"PathPlanner: esperaba {rows * cols} celdas, pero hay {cells.Length}.");
            return false;
        }

        state = new CellPaintMode[rows, cols];
        Vector2Int? startOpt = null;

        var targetsWithOrder = new List<(int order, Vector2Int pos)>();

        for (int i = 0; i < cells.Length; i++)
        {
            int x = i / cols;
            int y = i % cols;

            state[x, y] = cells[i].state;

            if (cells[i].state == CellPaintMode.Robot) startOpt = new Vector2Int(x, y);
            if (cells[i].state == CellPaintMode.Box) boxes.Add(new Vector2Int(x, y));

            if (cells[i].state == CellPaintMode.Target)
            {
                int ord = (cells[i].targetOrder >= 0) ? cells[i].targetOrder : int.MaxValue;
                targetsWithOrder.Add((ord, new Vector2Int(x, y)));
            }
        }

        if (!startOpt.HasValue || boxes.Count == 0 || targetsWithOrder.Count == 0)
        {
            Debug.LogError($"PathPlanner: faltan celdas. start={startOpt}, boxes={boxes.Count}, targets={targetsWithOrder.Count}");
            return false;
        }

        start = startOpt.Value;

        targetsWithOrder.Sort((a, b) => a.order.CompareTo(b.order));
        foreach (var t in targetsWithOrder) targetsByPriority.Add(t.pos);

        return true;
    }

    // -------------------------
    // A: NearestTarget (target más cercano por A*)
    // -------------------------
    private List<PlanAction> BuildPlan_NearestTarget()
    {
        if (!ReadGrid(out var state, out var start, out var boxes, out var targets, out int rows, out int cols))
            return null;

        var actions = new List<PlanAction>();
        Vector2Int currentPos = start;

        while (boxes.Count > 0)
        {
            if (!TryPickNearestBox(currentPos, boxes, state, rows, cols, out var nextBox, out var pathToBox))
            {
                Debug.LogError("NearestTarget: no hay ruta desde robot a ninguna Box.");
                return null;
            }

            if (!TryPickNearestTargetFromBox(nextBox, targets, state, rows, cols, out var chosenTarget, out var pathToTarget))
            {
                Debug.LogError("NearestTarget: no hay ruta desde la Box a ningún Target.");
                return null;
            }

            actions.AddRange(PathToActions(pathToBox));
            actions.Add(PlanAction.Pick);
            actions.AddRange(PathToActions(pathToTarget));
            actions.Add(PlanAction.Drop);

            currentPos = chosenTarget;
            boxes.Remove(nextBox);
        }

        return actions;
    }

    private bool TryPickNearestTargetFromBox(
        Vector2Int fromBox,
        List<Vector2Int> targets,
        CellPaintMode[,] state,
        int rows,
        int cols,
        out Vector2Int chosenTarget,
        out List<Vector2Int> chosenPath)
    {
        chosenTarget = default;
        chosenPath = null;
        int bestLen = int.MaxValue;

        foreach (var t in targets)
        {
            var p = AStar(fromBox, t, state, rows, cols);
            if (p == null) continue;

            if (p.Count < bestLen)
            {
                bestLen = p.Count;
                chosenTarget = t;
                chosenPath = p;
            }
        }

        return chosenPath != null;
    }

    // -------------------------
    // B: FixedPriority (target 1 salvo bloqueado -> siguiente)
    // -------------------------
    private List<PlanAction> BuildPlan_FixedPriority()
    {
        if (!ReadGrid(out var state, out var start, out var boxes, out var targetsByPriority, out int rows, out int cols))
            return null;

        var actions = new List<PlanAction>();
        Vector2Int currentPos = start;

        while (boxes.Count > 0)
        {
            if (!TryPickNearestBox(currentPos, boxes, state, rows, cols, out var nextBox, out var pathToBox))
            {
                Debug.LogError("FixedPriority: no hay ruta desde robot a ninguna Box.");
                return null;
            }

            if (!TryPickFirstReachableTarget(nextBox, targetsByPriority, state, rows, cols, out var chosenTarget, out var pathToTarget))
            {
                Debug.LogError("FixedPriority: todos los targets están bloqueados desde la Box.");
                return null;
            }

            actions.AddRange(PathToActions(pathToBox));
            actions.Add(PlanAction.Pick);
            actions.AddRange(PathToActions(pathToTarget));
            actions.Add(PlanAction.Drop);

            currentPos = chosenTarget;
            boxes.Remove(nextBox);
        }

        return actions;
    }

    private bool TryPickFirstReachableTarget(
        Vector2Int fromBox,
        List<Vector2Int> targetsByPriority,
        CellPaintMode[,] state,
        int rows,
        int cols,
        out Vector2Int chosenTarget,
        out List<Vector2Int> chosenPath)
    {
        for (int i = 0; i < targetsByPriority.Count; i++)
        {
            var t = targetsByPriority[i];
            var p = AStar(fromBox, t, state, rows, cols);
            if (p != null)
            {
                chosenTarget = t;
                chosenPath = p;
                return true;
            }
        }

        chosenTarget = default;
        chosenPath = null;
        return false;
    }

    // -------------------------
    // C: IdMapping (BoxID -> TargetID fijo)
    // -------------------------
    private List<PlanAction> BuildPlan_IdMapping()
    {
        if (!ReadGrid(out var state, out var start, out var boxes, out var targetsByPriority, out int rows, out int cols))
            return null;

        // Leer también boxOrder y targetOrder reales desde GridCell
        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();

        // pos caja -> boxOrder
        var boxOrderByPos = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < cells.Length; i++)
        {
            int x = i / cols;
            int y = i % cols;
            if (cells[i].state == CellPaintMode.Box && cells[i].boxOrder >= 0)
                boxOrderByPos[new Vector2Int(x, y)] = cells[i].boxOrder;
        }

        // targetOrder -> pos target
        var targetPosByOrder = new Dictionary<int, Vector2Int>();
        for (int i = 0; i < cells.Length; i++)
        {
            int x = i / cols;
            int y = i % cols;
            if (cells[i].state == CellPaintMode.Target && cells[i].targetOrder >= 0)
                targetPosByOrder[cells[i].targetOrder] = new Vector2Int(x, y);
        }

        // Verificación: cada box debe tener mapping y su target debe existir
        foreach (var b in boxes)
        {
            if (!boxOrderByPos.TryGetValue(b, out int bOrder))
            {
                Debug.LogError("IdMapping: hay una Box sin boxOrder.");
                return null;
            }

            if (!_idMapping.ContainsKey(bOrder))
            {
                Debug.LogError($"IdMapping: falta mapping para B{bOrder + 1}. Arrastra la caja al target correspondiente.");
                return null;
            }

            int tOrder = _idMapping[bOrder];
            if (!targetPosByOrder.ContainsKey(tOrder))
            {
                Debug.LogError($"IdMapping: el mapping de B{bOrder + 1} apunta a un Target {tOrder + 1} que no existe.");
                return null;
            }
        }

        var actions = new List<PlanAction>();
        Vector2Int currentPos = start;

        // Estrategia: entregar la caja más cercana, pero a su target mapeado
        while (boxes.Count > 0)
        {
            if (!TryPickNearestBox(currentPos, boxes, state, rows, cols, out var nextBox, out var pathToBox))
            {
                Debug.LogError("IdMapping: no hay ruta desde robot a ninguna Box.");
                return null;
            }

            int boxOrder = boxOrderByPos[nextBox];
            int targetOrder = _idMapping[boxOrder];
            Vector2Int targetPos = targetPosByOrder[targetOrder];

            var pathToTarget = AStar(nextBox, targetPos, state, rows, cols);
            if (pathToTarget == null)
            {
                Debug.LogError($"IdMapping: no hay ruta desde B{boxOrder + 1} hasta Target {targetOrder + 1}.");
                return null;
            }

            actions.AddRange(PathToActions(pathToBox));
            actions.Add(PlanAction.Pick);
            actions.AddRange(PathToActions(pathToTarget));
            actions.Add(PlanAction.Drop);

            currentPos = targetPos;
            boxes.Remove(nextBox);
        }

        return actions;
    }

    // -------------------------
    // Helper: caja más cercana por A*
    // -------------------------
    private bool TryPickNearestBox(
        Vector2Int fromPos,
        List<Vector2Int> boxes,
        CellPaintMode[,] state,
        int rows,
        int cols,
        out Vector2Int chosenBox,
        out List<Vector2Int> chosenPath)
    {
        chosenBox = default;
        chosenPath = null;
        int bestLen = int.MaxValue;

        foreach (var b in boxes)
        {
            var p = AStar(fromPos, b, state, rows, cols);
            if (p == null) continue;

            if (p.Count < bestLen)
            {
                bestLen = p.Count;
                chosenBox = b;
                chosenPath = p;
            }
        }

        return chosenPath != null;
    }

    // -------------------------
    // A* (4 vecinos)
    // -------------------------
    private List<Vector2Int> AStar(Vector2Int start, Vector2Int goal, CellPaintMode[,] grid, int rows, int cols)
    {
        bool IsBlocked(Vector2Int p) => grid[p.x, p.y] == CellPaintMode.Prohibited;
        int Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, goal) };

        while (open.Count > 0)
        {
            Vector2Int current = open[0];
            int bestF = fScore.TryGetValue(current, out var f0) ? f0 : int.MaxValue;

            for (int i = 1; i < open.Count; i++)
            {
                var n = open[i];
                int fn = fScore.TryGetValue(n, out var f) ? f : int.MaxValue;
                if (fn < bestF)
                {
                    bestF = fn;
                    current = n;
                }
            }

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            open.Remove(current);

            foreach (var neighbor in GetNeighbors4(current, rows, cols))
            {
                if (IsBlocked(neighbor)) continue;

                int tentativeG = gScore[current] + 1;

                if (!gScore.TryGetValue(neighbor, out int oldG) || tentativeG < oldG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);

                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private IEnumerable<Vector2Int> GetNeighbors4(Vector2Int p, int rows, int cols)
    {
        if (p.x - 1 >= 0) yield return new Vector2Int(p.x - 1, p.y);
        if (p.x + 1 < rows) yield return new Vector2Int(p.x + 1, p.y);
        if (p.y - 1 >= 0) yield return new Vector2Int(p.x, p.y - 1);
        if (p.y + 1 < cols) yield return new Vector2Int(p.x, p.y + 1);
    }

    private List<PlanAction> PathToActions(List<Vector2Int> path)
    {
        var actions = new List<PlanAction>();

        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1];
            var b = path[i];

            int dx = b.x - a.x;
            int dy = b.y - a.y;

            if (dx == -1 && dy == 0) actions.Add(PlanAction.MoveUp);
            else if (dx == 1 && dy == 0) actions.Add(PlanAction.MoveDown);
            else if (dx == 0 && dy == -1) actions.Add(PlanAction.MoveLeft);
            else if (dx == 0 && dy == 1) actions.Add(PlanAction.MoveRight);
            else Debug.LogWarning($"PathToActions: salto raro de {a} a {b}");
        }

        return actions;
    }

    public List<string> PlanToRobotCommands(List<PlanAction> plan)
    {
        var commands = new List<string>();
        Heading heading = Heading.Up;

        foreach (var action in plan)
        {
            switch (action)
            {
                case PlanAction.MoveUp:
                    TurnTo(ref heading, Heading.Up, commands);
                    commands.Add("FORWARD");
                    break;
                case PlanAction.MoveDown:
                    TurnTo(ref heading, Heading.Down, commands);
                    commands.Add("FORWARD");
                    break;
                case PlanAction.MoveLeft:
                    TurnTo(ref heading, Heading.Left, commands);
                    commands.Add("FORWARD");
                    break;
                case PlanAction.MoveRight:
                    TurnTo(ref heading, Heading.Right, commands);
                    commands.Add("FORWARD");
                    break;
                case PlanAction.Pick:
                    commands.Add("LIFTUP");
                    break;
                case PlanAction.Drop:
                    commands.Add("LIFTDOWN");
                    break;
            }
        }

        commands.Add("STOP");
        return commands;
    }

    private void TurnTo(ref Heading current, Heading target, List<string> commands)
    {
        int cur = (int)current;
        int tgt = (int)target;
        int diff = (tgt - cur + 4) % 4;

        if (diff == 1) { commands.Add("RIGHT"); current = target; }
        else if (diff == 3) { commands.Add("LEFT"); current = target; }
        else if (diff == 2) { commands.Add("RIGHT"); commands.Add("RIGHT"); current = target; }
    }

    public void AbortCurrentPath()
    {
        if (udpSender != null)
            udpSender.PausePlanAndStopRobot();
    }

    public void ResumeCurrentPath()
    {
        if (udpSender != null)
            udpSender.ResumePlan();
    }

    public List<Vector2Int> BuildRouteCells()
    {
        var plan = BuildPlan();
        if (plan == null) return null;

        if (!ReadGrid(out var state, out var start, out var boxes, out var targets, out int rows, out int cols))
            return null;

        var route = new List<Vector2Int>();
        var pos = start;
        route.Add(pos);

        foreach (var a in plan)
        {
            switch (a)
            {
                case PlanAction.MoveUp:    pos = new Vector2Int(pos.x - 1, pos.y); break;
                case PlanAction.MoveDown:  pos = new Vector2Int(pos.x + 1, pos.y); break;
                case PlanAction.MoveLeft:  pos = new Vector2Int(pos.x, pos.y - 1); break;
                case PlanAction.MoveRight: pos = new Vector2Int(pos.x, pos.y + 1); break;
                case PlanAction.Pick:
                case PlanAction.Drop:
                default:
                    break;
            }

            if (route[route.Count - 1] != pos)
                route.Add(pos);
        }

        return route;
    }

   [System.Serializable]
    public class PathSegment
    {
        public string label;              // "R → B1 → T2"
        public string boxLabel;           // "B1"
        public string targetLabel;        // "T2"

        public List<Vector2Int> cells;    // ruta completa (robot->box + box->target)
        public int pickIndex;             // índice dentro de cells donde se llega a la BOX (donde ocurre el PICK)
    }

    public List<PathSegment> BuildPathSegments()
    {
        if (!ReadGrid(out var state, out var start, out var boxes, out var targetsByPriority, out int rows, out int cols))
            return null;

        // Necesitamos orders reales para poner B1/T1 en la leyenda
        var cells = uiGrid.gridContainer.GetComponentsInChildren<GridCell>();
        var boxOrderByPos = new Dictionary<Vector2Int, int>();
        var targetOrderByPos = new Dictionary<Vector2Int, int>();

        for (int i = 0; i < cells.Length; i++)
        {
            int x = i / cols;
            int y = i % cols;
            var pos = new Vector2Int(x, y);

            if (cells[i].state == CellPaintMode.Box && cells[i].boxOrder >= 0)
                boxOrderByPos[pos] = cells[i].boxOrder;

            if (cells[i].state == CellPaintMode.Target && cells[i].targetOrder >= 0)
                targetOrderByPos[pos] = cells[i].targetOrder;
        }

        var segments = new List<PathSegment>();
        Vector2Int currentPos = start;

        // Helper: crea segmento Robot->Box y Box->Target (en una sola etiqueta)
        void AddSegment(Vector2Int boxPos, Vector2Int targetPos, List<Vector2Int> pathToBox, List<Vector2Int> pathToTarget)
        {
            // Unimos los paths en uno continuo para dibujar bonito
            var combined = new List<Vector2Int>();
            combined.AddRange(pathToBox);

            if (pathToTarget != null && pathToTarget.Count > 0)
            {
                // evita duplicar boxPos
                for (int i = 1; i < pathToTarget.Count; i++)
                    combined.Add(pathToTarget[i]);
            }

            // ✅ Etiqueta usando el TEXTO REAL que se ve en cada celda (R / B1 / T1)
            string rTxt = GetCellLabel(start, cells, cols);
            string bTxt = GetCellLabel(boxPos, cells, cols);
            string tTxt = GetCellLabel(targetPos, cells, cols);

            // rTxt, bTxt, tTxt ya los calculas como hacías
            int pickIndex = Mathf.Max(0, pathToBox.Count - 1);

            segments.Add(new PathSegment
            {
                label = $"{rTxt} → {bTxt} → {tTxt}",
                boxLabel = bTxt,
                targetLabel = tTxt,
                cells = combined,
                pickIndex = pickIndex
            });

            }


        // ======= SEGMENTACIÓN según modo =======
        while (boxes.Count > 0)
        {
                if (!TryPickNearestBox(currentPos, boxes, state, rows, cols, out var nextBox, out var pathToBox))
                    return null;

                Vector2Int chosenTarget = default;
                List<Vector2Int> pathToTarget = null;

                if (deliveryMode == DeliveryMode.NearestTarget)
                {
                    if (!TryPickNearestTargetFromBox(nextBox, targetsByPriority, state, rows, cols, out chosenTarget, out pathToTarget))
                        return null;
                }
                else if (deliveryMode == DeliveryMode.FixedPriority)
                {
                    if (!TryPickFirstReachableTarget(nextBox, targetsByPriority, state, rows, cols, out chosenTarget, out pathToTarget))
                        return null;
                }
                else if (deliveryMode == DeliveryMode.IdMapping)
                {
                    // Modo C: usa mapping fijo
                    if (!boxOrderByPos.TryGetValue(nextBox, out int bOrder)) return null;
                    if (!_idMapping.ContainsKey(bOrder)) return null;

                    int mappedTOrder = _idMapping[bOrder];

                    // buscar targetPos por order
                    Vector2Int targetPos = default;
                    bool found = false;
                    foreach (var kv in targetOrderByPos)
                    {
                        if (kv.Value == mappedTOrder) { targetPos = kv.Key; found = true; break; }
                    }
                    if (!found) return null;

                    chosenTarget = targetPos;
                    pathToTarget = AStar(nextBox, chosenTarget, state, rows, cols);
                    if (pathToTarget == null) return null;
                }
                else
                {
                    return null;
                }

                AddSegment(nextBox, chosenTarget, pathToBox, pathToTarget);

                currentPos = chosenTarget;
                boxes.Remove(nextBox);
        }

        return segments;
    }

    string GetCellLabel(Vector2Int pos, GridCell[] cells, int cols)
    {
        int idx = pos.x * cols + pos.y;
        if (idx < 0 || idx >= cells.Length) return "?";

        // Si tu GridCell tiene un TMP_Text público tipo "labelText" o similar,
        // cambia el nombre aquí.
        var tmp = cells[idx].GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();

        // Fallback por estado
        switch (cells[idx].state)
        {
            case CellPaintMode.Robot: return "R";
            case CellPaintMode.Box: return "B";
            case CellPaintMode.Target: return "T";
            default: return "?";
        }
    }



}
