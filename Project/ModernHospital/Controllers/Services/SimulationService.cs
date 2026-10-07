using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace LaboratoireProgrammation.Project.ModernHospital;

public class SimulationService : IDisposable {
    private readonly DatabaseManager _db;
    private DispatcherTimer? _clockTimer;
    private DateTime _gameTime;
    private DateTime _lastDoctorUpdate = DateTime.MinValue;
    private DateTime _lastPatientUpdate = DateTime.MinValue;
    private int _lastSimulatedDay = 1;
    private int _minutesToNextAutoAdmit = 180;
    private readonly Dictionary<int, float> _bedCleaningProgress = new();

    public DateTime GameTime => _gameTime;

    public event Action<DateTime>? OnTick;
    public event Action<string>? OnEventLog;
    public event Action? OnPersonnelChanged;
    public event Action? OnPatientsChanged;

    public SimulationService(DatabaseManager db) {
        _db = db;
        var hosp = _db.GetHospital();
        if (hosp != null) {
            if (TimeSpan.TryParse(hosp.HeureSimulation, out var ts)) {
                _gameTime = new DateTime(2026, 1, 1).AddDays(hosp.JourSimulation - 1).Add(ts);
            } else {
                _gameTime = new DateTime(2026, 1, 1).AddDays(hosp.JourSimulation - 1).AddHours(8);
            }
            _lastSimulatedDay = hosp.JourSimulation;
        } else {
            _gameTime = new DateTime(2026, 1, 1).AddHours(8);
            _lastSimulatedDay = 1;
        }
        _lastDoctorUpdate = _gameTime;
        _lastPatientUpdate = _gameTime;
    }

    private bool _isTicking;

    public void Start() {
        CatchUpSimulation();
        _clockTimer = new DispatcherTimer {
            Interval = TimeSpan.FromSeconds(HospitalSettings.Current.GameTickIntervalSeconds)
        };
        _clockTimer.Tick += async (s, e) => {
            if (_isTicking) return;
            _isTicking = true;
            try {
                _gameTime = _gameTime.AddSeconds(HospitalSettings.Current.GameTickIntervalSeconds * HospitalSettings.Current.TimeScaleMultiplier);
                OnTick?.Invoke(_gameTime);

                int currentDay = (_gameTime - new DateTime(2026, 1, 1)).Days + 1;
                var currentTime = _gameTime;
                await Task.Run(() => {
                    _db.SaveRealLifeTime(DateTime.Now, currentDay, currentTime.TimeOfDay);
                    UpdateDoctorsShifts();
                    UpdatePatientsTreatment();
                    CheckDayTransition();
                });
            }
            catch { }
            finally {
                _isTicking = false;
            }
        };
        _clockTimer.Start();
        
        OnTick?.Invoke(_gameTime);
        _ = Task.Run(() => {
            UpdateDoctorsShifts();
            UpdatePatientsTreatment();
            CheckDayTransition();
        });
    }

    public void Stop() {
        _clockTimer?.Stop();
    }

    private void UpdateDoctorsShifts() {
        if ((_gameTime - _lastDoctorUpdate).TotalMinutes < 1) return;
        int minutesPassed = (int)(_gameTime - _lastDoctorUpdate).TotalMinutes;
        _lastDoctorUpdate = _gameTime;
        UpdateDoctorsShiftsStep(minutesPassed);
    }

    private void UpdateDoctorsShiftsStep(int minutesPassed) {
        var docs = _db.GetPersonnel().Where(p => p.Categorie == "Corps Médical").ToList();
        bool changed = false;

        foreach (var doc in docs) {
            if (doc.Statut == "En poste") {
                doc.MinutesTravaillees += minutesPassed;
                if (doc.MinutesTravaillees >= doc.ProchainePauseMinutes) {
                    _db.UpdatePersonnelShiftData(doc.IdEmploye, "En pause", doc.MinutesTravaillees, 0, doc.MinutesHorsPoste);
                    changed = true;
                    OnEventLog?.Invoke($"☕ {doc.NomComplet} part en pause.");
                } else if (doc.MinutesTravaillees >= 600) {
                    _db.UpdatePersonnelShiftData(doc.IdEmploye, "Hors poste", 0, 0, 0);
                    changed = true;
                    OnEventLog?.Invoke($"🌙 {doc.NomComplet} a terminé son service (10h).");
                } else {
                    _db.UpdatePersonnelMinutes(doc.IdEmploye, "MinutesTravaillees", doc.MinutesTravaillees);
                }
            } else if (doc.Statut == "En pause") {
                doc.MinutesEnPause += minutesPassed;
                if (doc.MinutesEnPause >= 15) {
                    int nextPause = doc.MinutesTravaillees + 120;
                    _db.UpdatePersonnelShiftDataAndNextPause(doc.IdEmploye, "En poste", doc.MinutesTravaillees, 0, doc.MinutesHorsPoste, nextPause);
                    changed = true;
                    OnEventLog?.Invoke($"🩺 {doc.NomComplet} revient de pause.");
                } else {
                    _db.UpdatePersonnelMinutes(doc.IdEmploye, "MinutesEnPause", doc.MinutesEnPause);
                }
            } else if (doc.Statut == "Hors poste") {
                doc.MinutesHorsPoste += minutesPassed;
                if (doc.MinutesHorsPoste >= 720) {
                    _db.UpdatePersonnelShiftDataAndNextPause(doc.IdEmploye, "En poste", 0, 0, 0, 120);
                    changed = true;
                    OnEventLog?.Invoke($"☀️ {doc.NomComplet} commence son service.");
                } else {
                    _db.UpdatePersonnelMinutes(doc.IdEmploye, "MinutesHorsPoste", doc.MinutesHorsPoste);
                }
            }
        }

        OnPersonnelChanged?.Invoke();
    }

    private int _minutesToNextPatient = 60;
    private readonly Random _random = new();

    private static readonly string[] FirstNames = { 
        "Hugo", "Lucas", "Emma", "Léa", "Arthur", "Jules", "Louis", "Chloé", "Manon", "Inès",
        "Alice", "Thomas", "Paul", "Léo", "Sarah", "Gabriel", "Raphaël", "Camille", "Nathan", "Mathéo",
        "Clément", "Victor", "Antoine", "Enzo", "Maxime", "Alexandre", "Bastien", "Romain", "Théo", "Gabin",
        "Maël", "Noah", "Ethan", "Tiago", "Marius", "Axel", "Sacha", "Timothée", "Augustin", "Gaspard",
        "Baptiste", "Côme", "Léon", "Martin", "Oscar", "Simon", "Valentin", "Adam", "Amir", "Ibrahim",
        "Naël", "Rayane", "Yanis", "Jade", "Louise", "Ambre", "Alba", "Mia", "Rose", "Anna",
        "Léna", "Juliette", "Lina", "Lou", "Iris", "Julia", "Agathe", "Jeanne", "Lola", "Eva",
        "Mila", "Léonie", "Romane", "Margaux", "Nina", "Alix", "Zoé", "Lya", "Victoire", "Eden",
        "Mathilde", "Clara", "Olivia", "Charlie", "Elena", "Romy", "Capucine", "Sofia", "Lucie", "Éléonore",
        "Bérénice", "Célia", "Diane", "Estelle", "Fanny", "Garance", "Héloïse", "Isaline", "Justine", "Kenza",
        "Laurine", "Marion", "Noémie", "Océane", "Pauline", "Quitterie", "Roxane", "Solène", "Tessa", "Valentine",
        "Yasmine", "Apolline", "Constance", "Élise", "Flora", "Gabrielle", "Hortense", "Izia", "Joséphine", "Lise",
        "Madeleine", "Nora", "Pénélope", "Salomé", "Théa", "Violette", "Zélie", "Aya", "Fatima", "Inaya",
        "Nour", "Cédric", "Damien", "Fabien", "Guillaume", "Jérôme", "Laurent", "Julien", "Nicolas", "Olivier",
        "Pierre", "Sébastien", "Vincent", "Xavier", "Yann", "Benoît", "Christophe", "Denis", "Éric", "François",
        "Gilles", "Hervé", "Marc", "Patrick", "Stéphane", "Thierry", "Alain", "Bernard", "Christian", "Daniel",
        "Gérard", "Jacques", "Jean", "Michel", "Philippe", "René", "Adèle", "Aline", "Amélie", "Anaïs",
        "Aude", "Aurore", "Blandine", "Carole", "Céline", "Coline", "Coralie", "Delphine", "Élodie", "Émilie",
        "Flavie", "Mélanie", "Morgane", "Sabrina", "Sandrine", "Sophie", "Sylvie", "Valérie", "Véronique", "Virginie",
        "Béatrice", "Catherine", "Christine", "Corinne", "Isabelle", "Martine", "Nathalie", "Patricia", "Anne", "Marie"
    };

    private static readonly string[] LastNames = { 
        "Martin", "Bernard", "Thomas", "Petit", "Robert", "Richard", "Durand", "Dubois", "Moreau", "Laurent",
        "Simon", "Michel", "Lefebvre", "Leroy", "Roux", "David", "Bertrand", "Morel", "Fournier", "Girard",
        "Bonnet", "Dupont", "Lambert", "Fontaine", "Rousseau", "Vincent", "Muller", "Lefevre", "Faure", "Andre",
        "Mercier", "Blanc", "Guerin", "Boyer", "Garnier", "Chevalier", "Francois", "Legrand", "Gauthier", "Garcia",
        "Perrin", "Robin", "Clement", "Morin", "Nicolas", "Henry", "Roussel", "Mathieu", "Gautier", "Masson",
        "Marchand", "Duval", "Denis", "Dumont", "Marie", "Lemaire", "Noel", "Meyer", "Dufour", "Meunier",
        "Brun", "Blanchard", "Giraud", "Joly", "Riviere", "Lucas", "Brunet", "Gaillard", "Barbier", "Arnaud",
        "Martinez", "Gerard", "Roche", "Renard", "Schmitt", "Roy", "Leroux", "Colin", "Vidal", "Caron",
        "Picard", "Roger", "Fabre", "Aubert", "Lemoine", "Renaud", "Dumas", "Lacroix", "Olivier", "Philipp",
        "Bourgeois", "Pierre", "Benoit", "Rey", "Leclerc", "Payet", "Rolland", "Leclercq", "Guillaume", "Lecomte",
        "Lopez", "Jean", "Boutin", "Boucher", "Carpentier", "Menard", "Fleury", "Deschamps", "Poirier", "Pichon",
        "Boulanger", "Julien", "Berger", "Lopes", "Perron", "Maillard", "Moulin", "Dupuy", "Lombard", "Gillet",
        "Baron", "Leduc", "Sanchez", "Navarro", "Gomez", "Perez", "Costa", "Fernandez", "Rodriguez", "Morvan",
        "Lecocq", "Allard", "Pruvost", "Evrard", "Besson", "Cordier", "Laine", "Humbert", "Cousin", "Baudry",
        "Poulain", "Jacquet", "Reynaud", "Perrot", "Clerc", "Vasseur", "Villard", "Vallee", "Bodin", "Guillot",
        "Leblanc", "Collet", "Guichard", "Bouvier", "Blin", "Brossard", "Marty", "Poncet", "Vandamme", "Lebrun",
        "Besnard", "Gras", "Chevallier", "Bailly", "Lamy", "Pasquier", "Carlier", "Dossantos", "Lebreton", "Bouchet",
        "Breton", "Descamps", "Voisin", "Pineau", "Leveque", "Delattre", "Leger", "Delorme", "Guyot", "Neveu",
        "Jourdan", "Chartier", "Bousquet", "Gilbert", "Delannoy", "Bodinier", "Raimbault", "Gosselin", "Martel", "Bruneau",
        "Savary", "Pillet", "Blondel", "Maury", "Potier", "Guillard", "Texier", "Foucher", "Lesage", "Toussaint"
    };
    
    public event Action<string, string>? OnNotification;

    private void UpdatePatientsTreatment() {
        if ((_gameTime - _lastPatientUpdate).TotalMinutes < 1) return;
        int minutesPassed = (int)(_gameTime - _lastPatientUpdate).TotalMinutes;
        _lastPatientUpdate = _gameTime;
        UpdatePatientsTreatmentStep(minutesPassed);
        UpdateWaitingRoom(minutesPassed, false);
        UpdateMedicamentDeliveries(minutesPassed, false);
        UpdateCleaningRooms(minutesPassed);
        UpdateAdminAutoAdmit(minutesPassed);
    }

private void UpdatePatientsTreatmentStep(int minutesPassed) {
    float hoursPassed = minutesPassed / 60.0f;
    var activePatients = _db.GetPatientsRaw().Where(p => p.Statut == "En Diagnostic" || p.Statut == "En Traitement").ToList();
    var docs = _db.GetPersonnel().ToDictionary(d => d.IdEmploye, d => d);
    bool changed = false;

    float timeSpeedFactor = CalculateTimeSpeedFactor(activePatients.Count);
    float errorModifier = CalculateErrorModifier(activePatients.Count);

    foreach (var patient in activePatients) {
        if (ProcessSinglePatientTreatment(patient, hoursPassed, timeSpeedFactor, errorModifier, docs)) {
            changed = true;
        }
    }

    if (changed) {
        OnPatientsChanged?.Invoke();
    }
}

private float CalculateTimeSpeedFactor(int activePatientsCount) {
    int numAdmins = _db.GetActivePersonnelCountByRole("Agent Administratif");
    int numBrancardiers = _db.GetActivePersonnelCountByRole("Brancardier");
    int numActivePatients = Math.Max(1, activePatientsCount);

    float factor = 1.0f;
    if (numAdmins > 0) {
        factor *= 1f - (0.05f * numAdmins / numActivePatients);
    }
    if (numBrancardiers > 0) {
        factor *= 1f - (0.05f * numBrancardiers / numActivePatients);
    } else {
        factor *= 1f + (0.10f * numActivePatients / 1f);
    }

    return Math.Max(0.1f, factor);
}

private float CalculateErrorModifier(int activePatientsCount) {
    int numAdmins = _db.GetActivePersonnelCountByRole("Agent Administratif");
    int numBioTechs = _db.GetActivePersonnelCountByRole("Technicien Biomédical");
    int numMachines = Math.Max(1, _db.GetTotalEquipmentLevel());
    int numActivePatients = Math.Max(1, activePatientsCount);

    float modifier = 1.0f;
    if (numAdmins == 0) {
        modifier *= 1f + (0.02f * numActivePatients);
    }
    if (numBioTechs > 0) {
        modifier *= 1f - (0.08f * numBioTechs / numMachines);
    } else {
        modifier *= 1f + (0.15f * numMachines);
    }

    return Math.Max(0.1f, modifier);
}

private bool ProcessSinglePatientTreatment(PatientActif patient, float hoursPassed, float timeSpeedFactor, float errorModifier, Dictionary<int, Employe> docs) {
    bool docOnDuty = false;
    
    if (patient.IdMedecinAssigne.HasValue && docs.TryGetValue(patient.IdMedecinAssigne.Value, out var doc)) {
        if (doc.Statut == "En poste") {
            docOnDuty = true;
        }
    }

    if (!docOnDuty) {
        patient.TempsSansMedecin += hoursPassed;
        _db.UpdatePatientTempsSansMedecin(patient.IdPatient, patient.TempsSansMedecin);

        if (patient.TempsSansMedecin > 12.0f) {
            HandleNeglectedPatientDeath(patient);
            return true;
        }
        return false;
    }

    float effectiveHours = hoursPassed * timeSpeedFactor;
    float newTime = patient.TempsTraitementRestant - effectiveHours;

    if (newTime <= 0f) {
        FinalizeTreatment(patient);
        return true;
    }

    _db.UpdatePatientTreatmentTimeAndTempsSansMedecin(patient.IdPatient, newTime, patient.TempsSansMedecin);
    
    if (patient.Statut == "En Diagnostic" && newTime <= patient.TempsTraitementTotal * 0.8f) {
        TransitionToTreatmentPhase(patient, errorModifier);
        return true;
    }

    return false;
}

private void HandleNeglectedPatientDeath(PatientActif patient) {
    var fullPatient = _db.GetPatients().FirstOrDefault(p => p.IdPatient == patient.IdPatient);
    if (fullPatient != null) {
        PatientHelper.HandlePatientStatusChange(_db, fullPatient, "Décédé");
        OnEventLog?.Invoke($"☠ Patient {fullPatient.Nom} est décédé par manque de soins médicaux (Négligence).");
        OnNotification?.Invoke($"Décès par négligence : {fullPatient.Nom}", "Rouge");
    }
}

private void FinalizeTreatment(PatientActif patient) {
    _db.UpdatePatientTempsSansMedecin(patient.IdPatient, patient.TempsSansMedecin);
    
    var disease = _db.GetCasCliniques().FirstOrDefault(c => c.IdCas == patient.IdMaladie);
    if (disease != null && disease.CoutLogistique > 0) {
        _db.IncrementAccumulatedLogisticalFees(disease.CoutLogistique);
    }

    string outcome = _db.EvaluateTreatmentOutcome(patient.IdPatient);
    var fullPatient = _db.GetPatients().FirstOrDefault(p => p.IdPatient == patient.IdPatient);
    
    if (fullPatient != null) {
        PatientHelper.HandlePatientStatusChange(_db, fullPatient, outcome);
        if (outcome == "Guéri") {
            OnEventLog?.Invoke($"✨ Patient {fullPatient.Nom} est guéri après son traitement.");
            OnNotification?.Invoke($"Patient guéri : {fullPatient.Nom}", "Vert");
        } else {
            OnEventLog?.Invoke($"☠ Patient {fullPatient.Nom} est décédé.");
            OnNotification?.Invoke($"Patient décédé : {fullPatient.Nom}", "Rouge");
        }
    }
}

private void TransitionToTreatmentPhase(PatientActif patient, float errorModifier) {
    _db.UpdatePatientStatut(patient.IdPatient, "En Traitement");
    patient.Statut = "En Traitement";

    try {
        var disease = _db.GetCasCliniques().FirstOrDefault(c => c.IdCas == patient.IdMaladie);
        if (disease != null && patient.IdMedecinAssigne.HasValue) {
            var allDrugs = _db.GetMedicaments();
            var targetDrugs = allDrugs.Where(d => d.GetCiblesList().Any(c => c.IdCas == disease.IdCas)).ToList();
            var adverseDrugs = allDrugs.Where(d => d.GetIncompatiblesList().Any(i => i.IdCas == disease.IdCas)).ToList();

            Medicament? selectedDrug = null;
            int baseErrorChance = disease.RisqueErreurMedicale;
            int effectiveErrorChance = (int)Math.Min(100f, Math.Max(0f, baseErrorChance * errorModifier));
            int roll = _random.Next(1, 101);

            if (roll <= effectiveErrorChance && adverseDrugs.Count > 0) {
                selectedDrug = adverseDrugs[_random.Next(adverseDrugs.Count)];
            } else if (targetDrugs.Count > 0) {
                selectedDrug = targetDrugs[_random.Next(targetDrugs.Count)];
            }

            if (selectedDrug == null) {
                if (targetDrugs.Count > 0) selectedDrug = targetDrugs[_random.Next(targetDrugs.Count)];
                else if (adverseDrugs.Count > 0) selectedDrug = adverseDrugs[_random.Next(adverseDrugs.Count)];
            }

            if (selectedDrug != null) {
                int qte = selectedDrug.GetCiblesList().FirstOrDefault(c => c.IdCas == disease.IdCas)?.Quantite ?? selectedDrug.QuantitePrescriptionDefaut;
                _db.AddPrescription(patient.IdPatient, patient.IdMedecinAssigne.Value, selectedDrug.IdMedicament, qte, "1 dose par jour");
                OnEventLog?.Invoke($"💊 Le médecin a prescrit {qte}x {selectedDrug.Nom} pour {patient.Nom}.");

                var updatedDrug = _db.GetMedicamentById(selectedDrug.IdMedicament);
                if (updatedDrug != null) {
                    if (updatedDrug.StockActuel == 0) {
                        OnNotification?.Invoke($"Rupture totale : {updatedDrug.Nom} (0)", "Rouge");
                    } else if (updatedDrug.StockActuel < updatedDrug.StockMinimum) {
                        OnNotification?.Invoke($"Stock critique : {updatedDrug.Nom} ({updatedDrug.StockActuel})", "Ambre");
                    }
                }
            }
        }
    } catch (Exception ex) {
        OnEventLog?.Invoke($"⚠️ Erreur prescription automatique: {ex.Message}");
    }
}

    private void UpdateWaitingRoom(int minutesPassed, bool isOffline) {
        if (!isOffline) {
            _db.IncrementWaitingRoomPatientsTime(minutesPassed);
        }

        _minutesToNextPatient -= minutesPassed;
        while (_minutesToNextPatient <= 0) {
            string firstName = FirstNames[_random.Next(FirstNames.Length)];
            string lastName = LastNames[_random.Next(LastNames.Length)];
            var diseases = _db.GetCasCliniques();
            
            if (diseases.Count > 0) {
                var selectedDisease = diseases[_random.Next(diseases.Count)];
                
                if (isOffline) {
                    _db.GetWaitingRoomStats(out int gen, out int adm, out double ratio);
                    double threshold = gen > 0 ? ratio : 80.0;
                    double roll = _random.NextDouble() * 100.0;
                    _db.IncrementPatientsGeneres();
                    
                    if (roll <= threshold) {
                        int? freeBedId = _db.GetFirstFreeBedForUnit(selectedDisease.UniteRequise);
                        var medecins = _db.GetPersonnel().Where(p => p.Categorie == "Corps Médical" && p.Statut == "En poste").ToList();
                        if (medecins.Count == 0) {
                            medecins = _db.GetPersonnel().Where(p => p.Categorie == "Corps Médical").ToList();
                        }
                        int? medId = medecins.Count > 0 ? (int?)medecins[_random.Next(medecins.Count)].IdEmploye : null;
                        
                        if (freeBedId.HasValue) {
                            try {
                                _db.AdmitPatient($"{firstName} {lastName[0]}.", selectedDisease.IdCas, freeBedId.Value, medId, "Standard", selectedDisease.TempsTraitementHeures);
                                _db.IncrementPatientsAdmis();
                                OnEventLog?.Invoke($"🏨 [Hors-ligne] Patient admis automatiquement : {firstName} {lastName[0]}. ({selectedDisease.Maladie})");
                            } catch {}
                        } else {
                            _db.AddToVirtualQueue($"{firstName} {lastName[0]}.", selectedDisease.IdCas, medId, "Standard", (_gameTime - new DateTime(2026, 1, 1)).Days + 1);
                            OnEventLog?.Invoke($"⏳ [Hors-ligne] Aucun lit libre pour {firstName} {lastName[0]}. Placé en file d'attente virtuelle.");
                        }
                    } else {
                        OnEventLog?.Invoke($"💨 [Hors-ligne] Patient {firstName} {lastName[0]}. a manqué son admission.");
                    }
                } else {
                    _db.AddWaitingRoomPatient(lastName, firstName, selectedDisease.IdCas);
                    OnEventLog?.Invoke($"🟧 Nouveau patient en salle d'attente : {firstName} {lastName} ({selectedDisease.Symptomes})");
                    OnNotification?.Invoke($"Nouveau patient en salle d'attente : {firstName} {lastName}", "Ambre");
                }
            }
            _minutesToNextPatient += _random.Next(30, 241);
        }

        if (!isOffline) {
            var waitingPatients = _db.GetWaitingRoomPatients();
            foreach (var p in waitingPatients) {
                var cas = _db.GetCasCliniques().FirstOrDefault(c => c.IdCas == p.IdMaladie);
                if (cas != null) {
                    int patienceLimit = Math.Max(180, (int)(2 * cas.TempsTraitementHeures * 60));
                    if (p.TempsAttenteMinutes >= patienceLimit) {
                        _db.RemoveWaitingRoomPatient(p.IdPatientAttente);
                        OnEventLog?.Invoke($"⏳ Patient {p.NomComplet} s'est lassé d'attendre ({p.TempsAttenteMinutes}m) et est parti.");
                        OnNotification?.Invoke($"Patient parti (las d'attendre) : {p.NomComplet}", "Rouge");
                    }
                }
            }
        }

        var queue = _db.GetVirtualQueue();
        if (queue.Count > 0) {
            bool virtualQueueChanged = false;
            foreach (var q in queue) {
                var cas = _db.GetCasCliniques().FirstOrDefault(c => c.IdCas == q.IdMaladie);
                if (cas != null) {
                    int? freeBedId = _db.GetFirstFreeBedForUnit(cas.UniteRequise);
                    if (freeBedId.HasValue) {
                        try {
                            _db.AdmitPatient(q.Nom, q.IdMaladie, freeBedId.Value, q.IdMedecin, q.Classe, cas.TempsTraitementHeures);
                            _db.RemoveFromVirtualQueue(q.IdVirtual);
                            _db.IncrementPatientsAdmis();
                            virtualQueueChanged = true;
                            OnEventLog?.Invoke($"✅ Patient {q.Nom} de la file d'attente virtuelle a été assigné au lit.");
                            if (!isOffline) {
                                OnNotification?.Invoke($"Patient de la file virtuelle admis : {q.Nom}", "Vert");
                            }
                        } catch {}
                    }
                }
            }
            if (virtualQueueChanged && !isOffline) {
                OnPatientsChanged?.Invoke();
            }
        }
    }

    private void UpdateCleaningRooms(int minutesPassed) {
        var dirtyBeds = _db.GetLitsEnNettoyage();
        if (dirtyBeds.Count == 0) {
            _bedCleaningProgress.Clear();
            return;
        }

        foreach (var id in dirtyBeds) {
            if (!_bedCleaningProgress.ContainsKey(id))
                _bedCleaningProgress[id] = 0f;
        }
        var stale = _bedCleaningProgress.Keys.Where(k => !dirtyBeds.Contains(k)).ToList();
        foreach (var k in stale) _bedCleaningProgress.Remove(k);

        int numJanitors = _db.GetActivePersonnelCountByRole("Agent d'entretien");
        if (numJanitors == 0) return;

        int numDirty = dirtyBeds.Count;
        for (int i = 0; i < numDirty; i++) {
            int bedId = dirtyBeds[i];
            float janitorsHere = numJanitors >= numDirty
                ? (float)numJanitors / numDirty
                : (i < numJanitors ? 1f : 0f);

            _bedCleaningProgress[bedId] += minutesPassed * janitorsHere;

            if (_bedCleaningProgress[bedId] >= 60f) {
                _db.SetBedStatus(bedId, "Libre");
                _bedCleaningProgress.Remove(bedId);
                OnEventLog?.Invoke($"🧹 Lit #{bedId} nettoyé et disponible.");
                OnPatientsChanged?.Invoke();
            }
        }
    }

    private void UpdateAdminAutoAdmit(int minutesPassed) {
        _minutesToNextAutoAdmit -= minutesPassed;
        if (_minutesToNextAutoAdmit > 0) return;
        _minutesToNextAutoAdmit = 180;

        int numAdmins = _db.GetActivePersonnelCountByRole("Agent Administratif");
        if (numAdmins == 0) return;

        int admitted = 0;
        for (int i = 0; i < numAdmins; i++) {
            if (!_db.TryAutoAdmitFromWaitingRoom(_random)) break;
            admitted++;
            OnEventLog?.Invoke($"📋 Agent administratif : patient assigné automatiquement depuis la salle d'attente.");
            OnNotification?.Invoke("Patient assigné par l'administration", "Bleu");
        }
        if (admitted > 0) OnPatientsChanged?.Invoke();
    }

    private void UpdateMedicamentDeliveries(int minutesPassed, bool isOffline) {
        var deliveries = _db.GetPendingDeliveries();
        bool changed = false;
        foreach (var dev in deliveries) {
            int newTime = dev.TempsLivraisonRestant - minutesPassed;
            if (newTime <= 0) {
                _db.UpdateStock(dev.IdMedicament, dev.Quantite);
                _db.DeleteDelivery(dev.IdCommande);
                changed = true;
                
                var drug = _db.GetMedicamentById(dev.IdMedicament);
                if (drug != null) {
                    OnEventLog?.Invoke($"🧪 [Livraison] {dev.Quantite} unités de {drug.Nom} reçues.");
                    if (!isOffline) {
                        OnNotification?.Invoke($"Médicament livré : {dev.Quantite}x {drug.Nom}", "Bleu");
                    }
                }
            } else {
                _db.UpdateDeliveryTime(dev.IdCommande, newTime);
            }
        }
        if (changed && !isOffline) {
            OnPatientsChanged?.Invoke();
        }
    }

    private void CheckDayTransition() {
        int currentDay = (_gameTime - new DateTime(2026, 1, 1)).Days + 1;
        if (currentDay > _lastSimulatedDay) {
            for (int d = _lastSimulatedDay + 1; d <= currentDay; d++) {
                ExecuteDayTransition(d);
            }
            _lastSimulatedDay = currentDay;
        }
    }

    private void ExecuteDayTransition(int newDay) {
        var personnel = _db.GetPersonnel();
        decimal totalSalaries = 0;

        var lignesSalaires = new List<LigneFacture>();
        foreach (var emp in personnel) {
            totalSalaries += emp.SalaireJour;
            lignesSalaires.Add(new LigneFacture {
                TypePrestation = emp.RoleExact ?? emp.Categorie,
                Description = $"{emp.NomComplet} — {emp.RoleExact ?? emp.Categorie}",
                Quantite = 1,
                PrixUnitaire = emp.SalaireJour,
                SousTotal = emp.SalaireJour
            });
        }

        if (totalSalaries > 0) {
            _db.AddBudget(-totalSalaries, $"Salaires du personnel (Jour {newDay - 1})", newDay - 1, "Paiement Salaire");
            _db.CreateFactureHopital("Paiement Salaire", totalSalaries, $"Salaires versés au personnel hospitalier pour le Jour {newDay - 1}.", lignesSalaires);
        }

        var unites = _db.GetUnites();
        decimal totalMaintenance = 0;

        var lignesEntretien = new List<LigneFacture>();
        foreach (var u in unites) {
            totalMaintenance += u.CoutEntretienJour;
            lignesEntretien.Add(new LigneFacture {
                TypePrestation = "Entretien",
                Description = u.Nom,
                Quantite = 1,
                PrixUnitaire = u.CoutEntretienJour,
                SousTotal = u.CoutEntretienJour
            });
        }

        if (totalMaintenance > 0) {
            _db.AddBudget(-totalMaintenance, $"Frais d'entretien des services (Jour {newDay - 1})", newDay - 1, "Frais Entretien");
            _db.CreateFactureHopital("Frais Entretien", totalMaintenance, $"Frais de maintenance et d'entretien des services hospitaliers pour le Jour {newDay - 1}.", lignesEntretien);
        }

        decimal funeralFees = _db.GetAccumulatedFuneralFees();
        if (funeralFees > 0) {
            _db.AddBudget(-funeralFees, $"Services Funéraires (Jour {newDay - 1})", newDay - 1, "Frais Funéraires");
            _db.CreateFactureHopital("Frais Funéraires", funeralFees, $"Frais de services funéraires pour les cadavres qui n'ont pas pu être placés en Chambre froide le Jour {newDay - 1}.");
            _db.ResetAccumulatedFuneralFees();
        }

        decimal logisticalFees = _db.GetAccumulatedLogisticalFees();
        if (logisticalFees > 0) {
            _db.AddBudget(-logisticalFees, $"Frais Logistiques (Jour {newDay - 1})", newDay - 1, "Frais Logistiques");
            _db.CreateFactureHopital("Frais Logistiques", logisticalFees, $"Frais logistiques liés à l'utilisation du matériel et des technologies de diagnostic et traitement pour le Jour {newDay - 1}.");
            _db.ResetAccumulatedLogisticalFees();
        }

        _db.UpdateHospitalTime(newDay, _gameTime.TimeOfDay);
        OnEventLog?.Invoke($"📅 Jour {newDay} : Paie du personnel (-{totalSalaries:N0} $), Frais d'entretien (-{totalMaintenance:N0} $) & Frais logistiques (-{logisticalFees:N0} $)");

        var hosp = _db.GetHospital();
        if (hosp != null) {
            _db.ResolveExpiredProces(newDay, hosp.Budget, (nom, montant) => {
                OnEventLog?.Invoke($"⚖️ Procès perdu ({nom}) : -{montant:N0} $ déduits.");
                OnNotification?.Invoke($"Procès perdu : {nom}", "Rouge");
            });
        }
    }

    public void CatchUpSimulation() {
        var hosp = _db.GetHospital();
        if (hosp == null) return;

        var lastRealTime = _db.GetLastRealLifeTime();
        if (!lastRealTime.HasValue) return;

        double realSecondsPassed = (DateTime.Now - lastRealTime.Value).TotalSeconds;
        if (realSecondsPassed <= 0) return;

        double simSecondsPassed = realSecondsPassed * HospitalSettings.Current.TimeScaleMultiplier;
        int simMinutesPassed = (int)(simSecondsPassed / 60.0);

        if (simMinutesPassed <= 0) return;

        int maxOfflineMinutes = 4000;
        simMinutesPassed = Math.Min(simMinutesPassed, maxOfflineMinutes);

        OnEventLog?.Invoke($"⏳ Application fermée pendant {simMinutesPassed} minutes simulées. Rattrapage en cours...");

        ProcessOfflineMinutes(simMinutesPassed);

        int currentDay = (_gameTime - new DateTime(2026, 1, 1)).Days + 1;
        _db.SaveRealLifeTime(DateTime.Now, currentDay, _gameTime.TimeOfDay);
    
        OnEventLog?.Invoke($"✅ Rattrapage de la simulation terminé.");
    }
    
    private void ProcessOfflineMinutes(int totalMinutes) {
        int chunkSize = 10; 
        int remainingMinutes = totalMinutes;

        while (remainingMinutes > 0) {
            int step = Math.Min(chunkSize, remainingMinutes);
        
            SimulateTimeStep(step);
        
            remainingMinutes -= step;
        }
    }
    
    private void SimulateTimeStep(int minutes) {
        _gameTime = _gameTime.AddMinutes(minutes);
    
        UpdateDoctorsShiftsStep(minutes);
        UpdatePatientsTreatmentStep(minutes);
        UpdateWaitingRoom(minutes, true);
        UpdateMedicamentDeliveries(minutes, true);
        UpdateCleaningRooms(minutes);
        UpdateAdminAutoAdmit(minutes);
    
        CheckDayTransition();
    }

    public void Dispose() {
        Stop();
        _clockTimer = null;
    }
}
