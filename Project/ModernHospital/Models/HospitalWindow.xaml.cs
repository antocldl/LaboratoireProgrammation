using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;

namespace LaboratoireProgrammation.Project.ModernHospital;

public partial class HospitalWindow : Window {

    private readonly DatabaseManager _db = new();
    private readonly ObservableCollection<EventEntry> _eventLog = new();
    private readonly ObservableCollection<CartItem> _cart = new();
    private readonly ObservableCollection<ToastNotification> _notifications = new();
    
    private Button? _activeNavBtn;
    private ObservableCollection<ChambreGroup> _chambresGroupes = new();
    private HospitalInfo? _hospital;
    private SimulationService _simulationService;
    private MySqlConnector.MySqlDataAdapter? _currentAdapter;
    private MySqlConnector.MySqlCommandBuilder? _currentCommandBuilder;
    private System.Data.DataTable? _currentTable;
    private string _selectedTableName = "";

    public HospitalWindow() {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        LvCart.ItemsSource = _cart;
        IcToasts.ItemsSource = _notifications;

        HospitalSettings.Load();
        
        _simulationService = new SimulationService(_db);
        _simulationService.OnTick += t => {
            Dispatcher.Invoke(() => {
                TbDate.Text = t.ToString("dd/MM/yyyy");
                TbHeure.Text = t.ToString("HH:mm:ss");
                if (PanelSalleAttente != null && PanelSalleAttente.Visibility == Visibility.Visible) {
                    RefreshSalleAttente();
                }
            });
        };
        _simulationService.OnEventLog += msg => Dispatcher.Invoke(() => LogEvent(msg));
        _simulationService.OnNotification += (msg, col) => Dispatcher.Invoke(() => ShowToast(msg, col));
        _simulationService.OnPersonnelChanged += () => Dispatcher.Invoke(RefreshPersonnel);
        _simulationService.OnPatientsChanged += () => {
            Dispatcher.Invoke(() => {
                RefreshPatients();
                RefreshDashboard();
                RefreshFinances();
                if (PanelSalleAttente != null && PanelSalleAttente.Visibility == Visibility.Visible) {
                    RefreshSalleAttente();
                }
            });
        };
        Closing += (_, _) => {
            var simTime = _simulationService.GameTime;
            int simDay = (simTime - new DateTime(2026, 1, 1)).Days + 1;
            _db.SaveRealLifeTime(DateTime.Now, simDay, simTime.TimeOfDay);
            _simulationService.Dispose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        _ = InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync() {
        if (!_db.TestConnection()) {
            Dispatcher.Invoke(() => {
                LoadingPanel.Visibility = Visibility.Collapsed;
                MainPanel.Visibility = Visibility.Hidden;
                DbErrorPanel.Visibility = Visibility.Visible;
                SetStatus("❌ Connexion DB échouée : vérifiez vos identifiants", System.Windows.Media.Colors.OrangeRed);
            });
            return;
        }

        var steps = new List<(string Name, Action Action)> {
            ("Vérification des transactions...", () => _db.ExecuteCommand("ALTER TABLE Transactions_Financieres MODIFY COLUMN TypeTransaction ENUM('Revenu Patient', 'Paiement Salaire', 'Achat Équipement', 'Embauche', 'Frais Entretien', 'Remboursement Prêt', 'Facturation Patient', 'Dépense Construction', 'Frais Funéraires', 'Achat Médicaments', 'Frais Logistiques', 'Procès Perdu') NOT NULL")),
            ("Vérification de la structure des chambres (1/3)...", () => _db.ExecuteCommand("ALTER TABLE Chambre MODIFY COLUMN TypeChambre ENUM('Standard', 'Standard - Individuel', 'Standard - Commune', 'Soins Intensifs', 'Isolement', 'Bloc Opératoire') NOT NULL")),
            ("Vérification de la structure des chambres (2/3)...", () => _db.ExecuteCommand("UPDATE Chambre SET TypeChambre = 'Standard - Commune' WHERE TypeChambre = 'Standard'")),
            ("Vérification de la structure des chambres (3/3)...", () => _db.ExecuteCommand("ALTER TABLE Chambre MODIFY COLUMN TypeChambre ENUM('Standard - Individuel', 'Standard - Commune', 'Soins Intensifs', 'Isolement', 'Bloc Opératoire') NOT NULL")),
            ("Mise à jour du personnel (1/4)...", () => _db.ExecuteCommand("ALTER TABLE Personnel_Actif MODIFY COLUMN Statut ENUM('En poste', 'En pause', 'Absent', 'Épuisé', 'Hors poste') DEFAULT 'En poste'")),
            ("Mise à jour du personnel (2/4)...", () => _db.ExecuteCommand("ALTER TABLE Personnel_Actif ADD COLUMN MinutesTravaillees INT DEFAULT 0")),
            ("Mise à jour du personnel (3/4)...", () => _db.ExecuteCommand("ALTER TABLE Personnel_Actif ADD COLUMN MinutesEnPause INT DEFAULT 0")),
            ("Mise à jour du personnel (4/4)...", () => _db.ExecuteCommand("ALTER TABLE Personnel_Actif ADD COLUMN MinutesHorsPoste INT DEFAULT 0")),
            ("Mise à jour des cas cliniques (1/4)...", () => _db.ExecuteCommand("ALTER TABLE Cas_Cliniques MODIFY COLUMN TempsTraitementHeures FLOAT NOT NULL")),
            ("Mise à jour des patients actifs...", () => _db.ExecuteCommand("ALTER TABLE Patients_Actifs MODIFY COLUMN TempsTraitementRestant FLOAT DEFAULT 0")),
            ("Suivi du temps sans médecin...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Patients_Actifs ADD COLUMN TempsSansMedecin FLOAT DEFAULT 0"); } catch {}
            }),
            ("Mise à jour des cas cliniques (2/4)...", () => _db.ExecuteCommand("ALTER TABLE Cas_Cliniques ADD COLUMN TauxRemission FLOAT(5,2) DEFAULT 50.0")),
            ("Mise à jour des cas cliniques (3/4)...", () => _db.ExecuteCommand("ALTER TABLE Cas_Cliniques MODIFY COLUMN TauxRemission FLOAT(5,2) DEFAULT 50.0")),
            ("Mise à jour des cas cliniques (4/4)...", () => _db.ExecuteCommand("ALTER TABLE Cas_Cliniques ADD COLUMN SpecialisteTraitement VARCHAR(100)")),
            ("Initialisation de la persistance temporelle...", () => _db.ExecuteCommand("ALTER TABLE Hopital ADD COLUMN DerniereFermetureVraieVie DATETIME NULL")),
            ("Mise à jour du personnel (planning)...", () => _db.ExecuteCommand("ALTER TABLE Personnel_Actif ADD COLUMN ProchainePauseMinutes INT DEFAULT 120")),
            ("Mise à jour de la capacité des chambres...", () => _db.ExecuteCommand("ALTER TABLE Chambre ADD COLUMN CapaciteLits INT DEFAULT 2")),
            ("Configuration des unités requises...", () => _db.ExecuteCommand("ALTER TABLE Cas_Cliniques ADD COLUMN UniteRequise VARCHAR(100) NULL")),
            ("Mise à jour des cas cardiaques/urgences...", () => _db.ExecuteCommand("UPDATE Cas_Cliniques SET UniteRequise = 'Soins Intensifs Urgences' WHERE Maladie IN ('Infarctus du Myocarde', 'AVC Ischémique', 'Sepsis')")),
            ("Mise à jour des chirurgies...", () => _db.ExecuteCommand("UPDATE Cas_Cliniques SET UniteRequise = 'Bloc Opératoire A' WHERE Maladie = 'Appendicite Aiguë'")),
            ("Mise à jour des frais funéraires...", () => _db.ExecuteCommand("ALTER TABLE Hopital ADD COLUMN FraisFuneraireCumule DECIMAL(10,2) DEFAULT 0.00")),
            ("Configuration des prestations facturables...", () => _db.ExecuteCommand("ALTER TABLE LigneFacture MODIFY COLUMN TypePrestation ENUM('Chambre', 'Soin', 'Médicament', 'Autre') NOT NULL")),
            ("Configuration des coûts logistiques cliniques (1/2)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Cas_Cliniques ADD COLUMN CoutLogistique DECIMAL(10,2) DEFAULT 0.00"); } catch {}
            }),
            ("Configuration des coûts logistiques cliniques (2/2)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Hopital ADD COLUMN CoutLogistiqueCumule DECIMAL(10,2) DEFAULT 0.00"); } catch {}
            }),
            ("Création de la table Facture_Hopital...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Facture_Hopital (
                        IdFactureHopital INT AUTO_INCREMENT PRIMARY KEY,
                        CodeFacture VARCHAR(10) NOT NULL UNIQUE,
                        JourEmission INT NOT NULL,
                        TypeFacture VARCHAR(100) NOT NULL,
                        MontantTotal DECIMAL(10, 2) NOT NULL,
                        Description TEXT,
                        Statut VARCHAR(50) DEFAULT 'Payée'
                    )");
            }),
            ("Création de la table Frais_Supplementaires...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Frais_Supplementaires (
                        IdFrais INT AUTO_INCREMENT PRIMARY KEY,
                        IdPatient INT NOT NULL,
                        NomFrais VARCHAR(150) NOT NULL,
                        Montant DECIMAL(10, 2) NOT NULL,
                        FOREIGN KEY (IdPatient) REFERENCES Patients_Actifs(IdPatient) ON DELETE CASCADE
                    )");
            }),
            ("Création de la table Salle_Attente_Patients...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Salle_Attente_Patients (
                        IdPatientAttente INT AUTO_INCREMENT PRIMARY KEY,
                        Nom VARCHAR(50) NOT NULL,
                        Prenom VARCHAR(50) NOT NULL,
                        IdMaladie INT NOT NULL,
                        TempsAttenteMinutes INT DEFAULT 0,
                        FOREIGN KEY (IdMaladie) REFERENCES Cas_Cliniques(IdCas) ON DELETE CASCADE
                    )");
            }),
            ("Mise à jour de la pharmacie (1/4)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Medicament ADD COLUMN Maladies_Cibles TEXT"); } catch {}
            }),
            ("Mise à jour de la pharmacie (2/4)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Medicament ADD COLUMN Maladies_Incompatibles TEXT"); } catch {}
            }),
            ("Mise à jour de la pharmacie (3/4)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Medicament ADD COLUMN Temps_Livraison_Base INT DEFAULT 120"); } catch {}
            }),
            ("Mise à jour de la pharmacie (4/4)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Medicament ADD COLUMN Quantite_Prescription_Defaut INT DEFAULT 1"); } catch {}
            }),
            ("Création de la table Commandes_Medicaments...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Commandes_Medicaments (
                        IdCommande INT AUTO_INCREMENT PRIMARY KEY,
                        IdMedicament INT NOT NULL,
                        Quantite INT NOT NULL,
                        TempsLivraisonRestant INT NOT NULL,
                        PrixAchat DECIMAL(10,2) NOT NULL,
                        FOREIGN KEY (IdMedicament) REFERENCES Medicament(IdMedicament) ON DELETE CASCADE
                    )");
            }),
            ("Création de la table Virtual_Queue...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Virtual_Queue (
                        IdVirtual INT AUTO_INCREMENT PRIMARY KEY,
                        Nom VARCHAR(100) NOT NULL,
                        IdMaladie INT NOT NULL,
                        IdMedecin INT,
                        Classe VARCHAR(50) NOT NULL,
                        DateEntree INT NOT NULL,
                        FOREIGN KEY (IdMaladie) REFERENCES Cas_Cliniques(IdCas) ON DELETE CASCADE,
                        FOREIGN KEY (IdMedecin) REFERENCES Personnel_Actif(IdEmploye) ON DELETE SET NULL
                    )");
            }),
            ("Mise à jour des statistiques de la salle d'attente (1/2)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Hopital ADD COLUMN PatientsGeneres INT DEFAULT 0"); } catch {}
            }),
            ("Mise à jour des statistiques de la salle d'attente (2/2)...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Hopital ADD COLUMN PatientsAdmis INT DEFAULT 0"); } catch {}
            }),
            ("Mise à jour des codes factures...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Facture ADD COLUMN CodeFacture VARCHAR(10)"); } catch {}
            }),
            ("Génération des codes factures uniques...", () => {
                try {
                    var invoices = _db.GetFactures();
                    foreach (var inv in invoices) {
                        if (string.IsNullOrEmpty(inv.CodeFacture) || inv.CodeFacture.Length < 6) {
                            string newCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                            _db.ExecuteCommand(
                                "UPDATE Facture SET CodeFacture = @code WHERE IdFacture = @id",
                                cmd => {
                                    cmd.Parameters.AddWithValue("@code", newCode);
                                    cmd.Parameters.AddWithValue("@id", inv.IdFacture);
                                }
                            );
                        }
                    }
                } catch {}
            }),
            ("Indexation des factures...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Facture ADD UNIQUE INDEX idx_code_facture (CodeFacture)"); } catch {}
            }),
            ("Initialisation des spécialités...", () => {
                _db.ExecuteCommand("CREATE TABLE IF NOT EXISTS Specialite (IdSpecialite INT AUTO_INCREMENT PRIMARY KEY, Nom VARCHAR(100) NOT NULL UNIQUE)");
                int specCount = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM Specialite");
                if (specCount == 0) {
                    string[] specs = { "Urgences", "Chirurgie", "Pathologie", "Cardiologie", "Oncologie", "Gériatrie", "Médecine Interne", "Neuroimagerie", "Traumatologie", "Médecine Nucléaire", "Prélèvements", "Chimiothérapie", "Logistique", "Général" };
                    foreach (var s in specs) {
                        try {
                            _db.ExecuteCommand("INSERT INTO Specialite (Nom) VALUES (@nom)", cmd => cmd.Parameters.AddWithValue("@nom", s));
                        } catch { }
                    }
                }
            }),
            ("Alignement des spécialités...", () => {
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Urgences' WHERE Specialite = 'Diagnostic'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Neuroimagerie' WHERE Specialite = 'Neurochirurgie'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Chirurgie' WHERE Specialite = 'Chirurgie Plastique'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Pathologie' WHERE Specialite = 'Pédiatrie'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Général' WHERE Specialite = 'Médecine Générale'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Général' WHERE Specialite = 'Obstétrique'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Général' WHERE Specialite = 'Gynécologie'");
                _db.ExecuteCommand("UPDATE To_Hire SET Specialite = 'Urgences' WHERE Specialite = 'Soins Intensifs'");
            }),
            ("Mise à jour des candidats non-médicaux...", () => {
                string[] obsoleteRoles = { "Régulateur Flux", "Gestionnaire Dossiers", "Support IT",
                    "Travailleur Social", "Représentant Patient", "Inteprète", "Interprète",
                    "Aumônier", "Agent EVS", "Gestionnaire Déchets", "Agent Sécurité", "Spécialiste Codage" };
                string inClause = string.Join(",", obsoleteRoles.Select(r => $"'{r}'"));
                try {
                    _db.ExecuteCommand($"DELETE FROM To_Hire WHERE RoleExact IN ({inClause})");
                    _db.ExecuteCommand($"DELETE FROM Personnel_Actif WHERE RoleExact IN ({inClause})");
                } catch { }

                int count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Agent Administratif'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('Beaumont', 'Claire', 'Administration', 'Agent Administratif', NULL, 300.00, 500.00), ('Dupin', 'Marc', 'Administration', 'Agent Administratif', NULL, 280.00, 450.00), ('Fontaine', 'Lise', 'Administration', 'Agent Administratif', NULL, 320.00, 600.00)");
                }
                count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Agent d\u0027entretien'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('Filch', 'Argus', 'Logistique & Support', 'Agent d\\'entretien', NULL, 200.00, 200.00), ('Muntz', 'Nelson', 'Logistique & Support', 'Agent d\\'entretien', NULL, 210.00, 250.00)");
                }
                count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Brancardier'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('Valjean', 'Jean', 'Logistique & Support', 'Brancardier', 'Transport', 250.00, 300.00), ('Javert', 'Inspecteur', 'Logistique & Support', 'Brancardier', 'Urgences', 280.00, 400.00)");
                }
                count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Technicien Biomédical'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('MacGyver', 'Angus', 'Logistique & Support', 'Technicien Biomédical', 'Maintenance', 600.00, 2500.00), ('Scott', 'Montgomery', 'Logistique & Support', 'Technicien Biomédical', 'Équipement lourd', 580.00, 2000.00)");
                }
                count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Comptable'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('Scrooge', 'Ebenezer', 'Administration', 'Comptable', 'Facturation', 500.00, 1500.00), ('Krabs', 'Eugene', 'Administration', 'Comptable', 'Assurances', 450.00, 1000.00)");
                }
                count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM To_Hire WHERE RoleExact = 'Avocat'");
                if (count == 0) {
                    _db.ExecuteCommand("INSERT INTO To_Hire (Nom, Prenom, Categorie, RoleExact, Specialite, SalaireJour, PrimeEmbauche) VALUES ('Murdock', 'Matt', 'Service Juridique', 'Avocat', 'Défense', 700.00, 3000.00), ('Walters', 'Jennifer', 'Service Juridique', 'Avocat', 'Médecine légale', 750.00, 3500.00)");
                }
            }),
            ("Création de la table Procès...", () => {
                _db.ExecuteCommand(@"
                    CREATE TABLE IF NOT EXISTS Proces (
                        IdProces INT AUTO_INCREMENT PRIMARY KEY,
                        NomPatient VARCHAR(100) NOT NULL,
                        NomMaladie VARCHAR(100) NOT NULL,
                        TauxRemission FLOAT NOT NULL,
                        JourOuverture INT NOT NULL,
                        JourFermeture INT NOT NULL,
                        Statut VARCHAR(20) DEFAULT 'En cours',
                        MontantPenalite DECIMAL(15,2) DEFAULT 0.00
                    )");
            }),
            ("Ajout du détail des factures hôpital...", () => {
                try { _db.ExecuteCommand("ALTER TABLE Facture_Hopital ADD COLUMN LignesJson MEDIUMTEXT NULL"); } catch {}
            })
        };

        await Task.Run(() => {
            for (int i = 0; i < steps.Count; i++) {
                var step = steps[i];
                int percentage = (int)(((double)(i + 1) / steps.Count) * 100);

                Dispatcher.Invoke(() => {
                    TbLoadingStep.Text = step.Name;
                    PbLoading.Value = percentage;
                });

                try {
                    step.Action();
                } catch { }

                System.Threading.Thread.Sleep(50);
            }
        });

        Dispatcher.Invoke(() => {
            LoadingPanel.Visibility = Visibility.Collapsed;
            RootGrid.Width = ActualWidth;
            RootGrid.Height = ActualHeight;

            IcEventLog.ItemsSource = _eventLog;

            SetStatus("✔️ Connexion établie", System.Windows.Media.Color.FromRgb(0, 212, 170));

            Nav_Click(BtnDashboard, null!);

            LoadAdmitForm();

            RefreshAll();
            _simulationService.Start();
        });
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
        RootGrid.Width = e.NewSize.Width;
        RootGrid.Height = e.NewSize.Height;
    }

    private void Nav_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString() ?? "";

        if (tag == "admin") {
            var pwdWindow = new AdminPasswordWindow();
            pwdWindow.Owner = this;
            if (pwdWindow.ShowDialog() != true) {
                return;
            }
        }

        if (_activeNavBtn != null) _activeNavBtn.Style = (Style)FindResource("NavBtn");
        btn.Style = (Style)FindResource("NavBtnActive");
        _activeNavBtn = btn;

        PanelDashboard.Visibility = Visibility.Collapsed;
        PanelPatients.Visibility = Visibility.Collapsed;
        PanelPersonnel.Visibility = Visibility.Collapsed;
        PanelFinances.Visibility = Visibility.Collapsed;
        PanelChambres.Visibility = Visibility.Collapsed;
        PanelMedicaments.Visibility = Visibility.Collapsed;
        PanelFacturation.Visibility = Visibility.Collapsed;
        PanelHitParade.Visibility = Visibility.Collapsed;
        PanelReservations.Visibility = Visibility.Collapsed;
        PanelSalleAttente.Visibility = Visibility.Collapsed;
        PanelAdmin.Visibility = Visibility.Collapsed;
        PanelProces.Visibility = Visibility.Collapsed;

        switch (tag) {
            case "dashboard":
                PanelDashboard.Visibility = Visibility.Visible;
                RefreshDashboard();
                break;
            case "patients":
                PanelPatients.Visibility = Visibility.Visible;
                RefreshPatients();
                LoadAdmitForm();
                break;
            case "personnel":
                PanelPersonnel.Visibility = Visibility.Visible;
                RefreshPersonnel();
                break;
            case "finances":
                PanelFinances.Visibility = Visibility.Visible;
                RefreshFinances();
                break;
            case "chambres":
                PanelChambres.Visibility = Visibility.Visible;
                RefreshChambres();
                break;
            case "medicaments":
                PanelMedicaments.Visibility = Visibility.Visible;
                RefreshMedicaments();
                break;
            case "facturation":
                PanelFacturation.Visibility = Visibility.Visible;
                RefreshFactures();
                break;
            case "hitparade":
                PanelHitParade.Visibility = Visibility.Visible;
                RefreshHitParade();
                break;
            case "reservations":
                PanelReservations.Visibility = Visibility.Visible;
                RefreshReservations();
                break;
            case "salleattente":
                PanelSalleAttente.Visibility = Visibility.Visible;
                RefreshSalleAttente();
                break;
            case "admin":
                PanelAdmin.Visibility = Visibility.Visible;
                if (CbAdminTables.SelectedIndex == -1) {
                    CbAdminTables.SelectedIndex = 0;
                } else {
                    var selectedItem = (ComboBoxItem)CbAdminTables.SelectedItem;
                    LoadTableData(selectedItem.Content.ToString() ?? "");
                }
                break;
            case "proces":
                PanelProces.Visibility = Visibility.Visible;
                RefreshProces();
                break;
        }
    }

    private void RefreshAll() {
        _hospital = _db.GetHospital();
        if (_hospital == null) return;
        RefreshSidebar();
        RefreshDashboard();
    }

    private void RefreshSidebar() {
        if (_hospital == null) return;
        TbBudget.Text = $"$ {_hospital.Budget:N0}";
    }

    private void RefreshDashboard() {
        _hospital = _db.GetHospital();
        if (_hospital == null) return;
        RefreshSidebar();

        var stats = _db.GetStats();
        KpiPatientsActifs.Text = stats.TotalPatientsActifs.ToString();
        KpiAttente.Text = $"{stats.PatientsEnAttente} en attente";
        KpiPersonnel.Text = stats.TotalPersonnel.ToString();
        KpiLitsLibres.Text = stats.LitsLibres.ToString();
        KpiLitsOccupes.Text = $"{stats.LitsOccupes} occupés";
        KpiRevenu.Text = $"+ {stats.RevenuJour:N0} $";
        KpiDepenses.Text = $"- {stats.DepensesJour:N0} $";
        
        if (stats.RevenuJour > stats.DepensesJour) KpiTotal.Text = $"+ {stats.RevenuJour - stats.DepensesJour:N0} $";
        else if (stats.RevenuJour < stats.DepensesJour) KpiTotal.Text = $"- {Math.Abs(stats.RevenuJour - stats.DepensesJour):N0} $";
        else KpiTotal.Text = "0 $";
        
        KpiGueris.Text = stats.PatientsGueris.ToString();
        KpiDeces.Text = stats.PatientsDeces.ToString();
        KpiBudget.Text = $"${_hospital.Budget:N0}";
        KpiTraitement.Text = stats.PatientsEnTraitement.ToString();
    }

    private void RefreshPatients() {
        var patients = _db.GetPatients();
        LvPatients.ItemsSource = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(patients, p => p.Statut != "Guéri" && p.Statut != "Décédé" && p.Statut != "Réservé"));
        LvPatientsHistorique.ItemsSource = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(patients, p => p.Statut == "Guéri" || p.Statut == "Décédé"));
    }

    private void RefreshPersonnel() {
        Dispatcher.Invoke(() => {
            LvPersonnel.ItemsSource = _db.GetPersonnel();
            LvCandidats.ItemsSource = _db.GetCandidats();
        });
    }

    private void RefreshChambres() {
        _chambresGroupes = _db.GetChambresHierarchiques();
        IcChambres.ItemsSource = _chambresGroupes;
    }

    private void RefreshFinances() {
        LvTransactions.ItemsSource = _db.GetRecentTransactions(50);
    }

    private void RefreshAllData() {
        RefreshPatients();
        RefreshPersonnel();
        RefreshChambres();
        RefreshDashboard();
        RefreshFinances();
    }

    private void LoadAdmitForm() {
        var casCliniques = _db.GetCasCliniques();
        var litsLibres = _db.GetLitsLibres();
        var medecins = _db.GetPersonnel().Where(p => p.Categorie == "Corps Médical").ToList();

        CbMaladie.ItemsSource = casCliniques;
        CbMaladie.SelectedIndex = 0;
        CbLit.ItemsSource = litsLibres;
        CbLit.SelectedIndex = 0;
        CbMedecin.ItemsSource = medecins;
        CbMedecin.SelectedIndex = 0;
        CbClasse.SelectedIndex = 0;

        CbResMaladie.ItemsSource = casCliniques;
        CbResMaladie.SelectedIndex = 0;
        CbResLit.ItemsSource = litsLibres;
        CbResLit.SelectedIndex = 0;
        CbResMedecin.ItemsSource = medecins;
        CbResMedecin.SelectedIndex = 0;
        CbResClasse.SelectedIndex = 0;
    }

    private void BtnReserver_Click(object sender, RoutedEventArgs e) {
        if (CbResMaladie.SelectedItem == null || CbResLit.SelectedItem == null || CbResMedecin.SelectedItem == null) {
            PopupFactory.ShowAlert(this, "Veuillez sélectionner la maladie, le lit et le médecin pour la réservation.");
            return;
        }

        string nom = TbResPatientNom.Text;
        if (string.IsNullOrWhiteSpace(nom)) {
            PopupFactory.ShowAlert(this, "Veuillez entrer un nom de patient valide.");
            return;
        }

        var maladie = (CasClinique)CbResMaladie.SelectedItem;
        var lit = (LitDisponible)CbResLit.SelectedItem;
        var medecin = (Employe)CbResMedecin.SelectedItem;
        string classe = ((ComboBoxItem)CbResClasse.SelectedItem).Content.ToString();

        try {
            _db.ReservePatient(nom, maladie.IdCas, lit.IdLit, medecin.IdEmploye, classe, maladie.TempsTraitementHeures);

            LogEvent($"📅 Réservation enregistrée pour {nom} au lit {lit.NumeroLit}");
            TbResPatientNom.Text = "";
            LoadAdmitForm();
            RefreshPatients();
            RefreshReservations();
            RefreshDashboard();
            SetStatus($"✔️ Réservation effectuée avec succès", Color.FromRgb(0, 212, 170));
        }
        catch (Exception ex) {
            SetStatus($"❌ Erreur réservation: {ex.Message}", Colors.OrangeRed);
        }
    }

    private void BtnAdmettre_Click(object sender, RoutedEventArgs e) {
        if (_hospital == null) return;
        if (CbMaladie.SelectedItem is not CasClinique cas) {
            ShowAlert("Sélectionnez une maladie.");
            return;
        }

        if (CbLit.SelectedItem is not LitDisponible lit) {
            ShowAlert("Aucun lit disponible.");
            return;
        }

        var nom = TbPatientNom.Text.Trim();
        if (string.IsNullOrEmpty(nom)) {
            ShowAlert("Entrez un nom de patient.");
            return;
        }

        var idMedecin = (CbMedecin.SelectedItem as Employe)?.IdEmploye;
        var classe = (CbClasse.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Standard";

        try {
            _db.AdmitPatient(nom, cas.IdCas, lit.IdLit, idMedecin, classe, cas.TempsTraitementHeures);
            LogEvent($"🏥 Patient admis: {nom} ({cas.Maladie}) : Lit {lit.NumeroLit}");
            RefreshPatients();
            LoadAdmitForm();
            RefreshDashboard();
            SetStatus($"✅ Patient {nom} admis avec succès", Color.FromRgb(0, 212, 170));
        }
        catch (Exception ex) {
            SetStatus($"❌ Erreur admission: {ex.Message}", Colors.OrangeRed);
        }
    }

    private void BtnFire_Click(object sender, RoutedEventArgs e) {
        if (LvPersonnel.SelectedItem is not Employe emp) {
            ShowAlert("Sélectionnez un employé.");
            return;
        }

        var firePopup = PopupFactory.CreateConfirmationPopup(
            $"Êtes-vous sûr de vouloir licencier {emp.NomComplet} ?",
            "#FF4D6A",
            () => {
                _db.FireEmploye(emp.IdEmploye);
                LogEvent($"🗑 {emp.NomComplet} licencié(e)");
                RefreshPersonnel();
                RefreshDashboard();
                SetStatus($"✅ {emp.NomComplet} a été licencié", Color.FromRgb(255, 77, 106));
            },
            () => { }
        );

        firePopup.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        firePopup.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnHire_Click(object sender, RoutedEventArgs e) {
        var doctorForm = new DoctorCreationForm(_db) {
            OnDoctorCreated = () => {
                RefreshPersonnel();
                RefreshDashboard();
                SetStatus("✅ Médecin créé avec succès", Color.FromRgb(0, 212, 170));
                LogEvent("👨‍⚕️ Nouveau médecin créé");
            },
            OnCancelled = () => { }
        };
        doctorForm.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        doctorForm.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnRecruterCandidat_Click(object sender, RoutedEventArgs e) {
        if (_hospital == null) return;
        if (sender is Button btn && btn.Tag is Candidat candidat) {
            if (_hospital.Budget < candidat.PrimeEmbauche) {
                PopupFactory.ShowAlert(this, $"Budget insuffisant pour embaucher {candidat.NomComplet}. Prime requise: ${candidat.PrimeEmbauche:N0}");
                return;
            }

            var confirm = PopupFactory.CreateConfirmationPopup(
                $"Recruter {candidat.NomComplet} (Prime: ${candidat.PrimeEmbauche:N0}, Salaire: ${candidat.SalaireJour}/j) ?",
                "#00d4aa",
                () => {
                    bool success = _db.HireCandidat(candidat.IdCandidat, _hospital);
                    if (success) {
                        LogEvent($"🤝 {candidat.NomComplet} embauché(e).");
                        RefreshPersonnel();
                        RefreshDashboard();
                        SetStatus($"✔️ {candidat.NomComplet} embauché avec succès", Color.FromRgb(0, 212, 170));
                    } else {
                        PopupFactory.ShowAlert(this, "Erreur lors de l'embauche.");
                    }
                }
            );
            confirm.Owner = this;
            DimOverlay.Visibility = Visibility.Visible;
            confirm.ShowDialog();
            DimOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshPatients_Click(object sender, RoutedEventArgs e) {
        RefreshPatients();
    }

    private void BtnVoirReservations_Click(object sender, RoutedEventArgs e) {
        var win = new ViewReservationsWindow(_db);
        win.Owner = this;
        if (win.ShowDialog() == true) {
            RefreshPatients();
            RefreshReservations();
            RefreshDashboard();
        }
    }

    private void RefreshFinances_Click(object sender, RoutedEventArgs e) {
        RefreshFinances();
    }

    private void LogEvent(string msg) {
        var category = CategorizeEvent(msg);
        _eventLog.Insert(0, new EventEntry { Message = msg, Category = category });
        if (_eventLog.Count > 100) _eventLog.RemoveAt(_eventLog.Count - 1);
        TbLastEvent.Text = msg;
        ApplyEventFilter();
    }

    private void ApplyEventFilter() {
        string selectedFilter = "All";
        if (RbFilterPatient.IsChecked == true) selectedFilter = "Patient";
        else if (RbFilterMedecin.IsChecked == true) selectedFilter = "Medecin";
        else if (RbFilterLogistique.IsChecked == true) selectedFilter = "Logistique";

        if (selectedFilter == "All") {
            IcEventLog.ItemsSource = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(_eventLog, e => e.Message));
        } else {
            IcEventLog.ItemsSource = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(_eventLog, e => e.Category == selectedFilter), e => e.Message));
        }
    }

    private void RbFilter_Click(object sender, RoutedEventArgs e) {
        ApplyEventFilter();
    }

    private string CategorizeEvent(string msg) {
        if (msg.Contains("☕") || msg.Contains("🩺") || msg.Contains("🌙") || msg.Contains("☀️") || (msg.Contains("🗑") && msg.Contains("licencié")) || msg.Contains("👨‍⚕️") || msg.Contains("🤝")) {
            return "Medecin";
        }
        if (msg.Contains("✨") || msg.Contains("☠") || msg.Contains("📅") || msg.Contains("🏥") || msg.Contains("Patient")) {
            return "Patient";
        }
        return "Logistique";
    }

    private void SetStatus(string msg, Color color) {
        TbStatus.Text = msg;
        TbStatus.Foreground = new SolidColorBrush(color);
    }

    private void ShowAlert(string message) {
        var alertPopup = PopupFactory.CreateConfirmationPopup(
            message, "#8223c2", () => { }, () => { }
        );

        alertPopup.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        alertPopup.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void LvPatients_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
        if (LvPatients.SelectedItem is PatientActif patient) {
            var menu = new PatientActionMenu(patient) { Owner = this };
            menu.ActionTriggered += (act) => HandlePatientAction(patient, act);

            var mousePos = PointToScreen(Mouse.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null) {
                var matrix = source.CompositionTarget.TransformToDevice;
                menu.Left = mousePos.X / matrix.M11;
                menu.Top = mousePos.Y / matrix.M22;
            } else {
                menu.Left = mousePos.X;
                menu.Top = mousePos.Y;
            }
            menu.Show();
        }
    }

    private void HandlePatientAction(PatientActif patient, string action) {
        switch (action) {
            case "Gueri":
                PatientHelper.HandlePatientStatusChange(_db, patient, "Guéri");
                LogEvent($"✨ Patient guéri: {patient.Nom}");
                break;
            case "Decede":
                PatientHelper.HandlePatientStatusChange(_db, patient, "Décédé");
                LogEvent($"☠ Patient décédé: {patient.Nom}");
                break;
            case "Supprimer":
                _db.ReleaseBed(patient.IdPatient);
                _db.DeletePatient(patient.IdPatient);
                LogEvent($"🗑 Patient supprimé: {patient.Nom}");
                break;
            case "Prescrire":
                int docId = patient.IdMedecinAssigne ?? _db.GetPersonnel().FirstOrDefault(p => p.Categorie == "Corps Médical")?.IdEmploye ?? 1;
                var presForm = new AddPrescriptionForm(_db, patient.IdPatient, docId) { Owner = this };
                if (presForm.ShowDialog() == true) {
                    LogEvent($"📋 Prescription ajoutée pour {patient.Nom}");
                    RefreshMedicaments();
                }
                break;
            case "AssignerMedecin":
                var assignForm = new AssignDoctorWindow(_db, patient.IdPatient) { Owner = this };
                if (assignForm.ShowDialog() == true) {
                    LogEvent($"🩺 Médecin assigné pour {patient.Nom}");
                }
                break;
            case "FraisSupplementaires":
                var extraFeeForm = new AddExtraFeeWindow(patient.IdPatient) { Owner = this };
                if (extraFeeForm.ShowDialog() == true) {
                    LogEvent($"💰 Frais supplémentaire ajouté pour {patient.Nom} : {extraFeeForm.FeeName} ({extraFeeForm.FeeAmount} $)");
                }
                break;
            case "Facture":
                Nav_Click(BtnFacturation, null!);
                break;
            default:
                PatientHelper.HandlePatientStatusChange(_db, patient, action);
                break;
        }

        RefreshAllData();
    }

    private void LvPersonnel_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
        if (LvPersonnel.SelectedItem is Employe emp) {
            var menu = new PersonnelActionMenu(emp) { Owner = this };
            menu.ActionTriggered += (act) => HandlePersonnelAction(emp, act);

            var mousePos = PointToScreen(Mouse.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null) {
                var matrix = source.CompositionTarget.TransformToDevice;
                menu.Left = mousePos.X / matrix.M11;
                menu.Top = mousePos.Y / matrix.M22;
            } else {
                menu.Left = mousePos.X;
                menu.Top = mousePos.Y;
            }
            menu.Show();
        }
    }

    private void HandlePersonnelAction(Employe emp, string action) {
        if (action == "Licencier") {
            var firePopup = PopupFactory.CreateConfirmationPopup(
                $"Êtes-vous sûr de vouloir licencier {emp.NomComplet} ?",
                "#FF4D6A",
                () => {
                    _db.FireEmploye(emp.IdEmploye);
                    LogEvent($"🗑 {emp.NomComplet} licencié(e)");
                    RefreshPersonnel();
                    RefreshDashboard();
                    SetStatus($"✅ {emp.NomComplet} a été licencié", Color.FromRgb(255, 77, 106));
                },
                () => { }
            );
            firePopup.Owner = this;
            DimOverlay.Visibility = Visibility.Visible;
            firePopup.ShowDialog();
            DimOverlay.Visibility = Visibility.Collapsed;
        }
        else {
            _db.UpdatePersonnelStatus(emp.IdEmploye, action);
            RefreshPersonnel();
        }
    }

    private void BtnAddLit_Click(object sender, RoutedEventArgs e) {
        var addLitForm = new AddLitForm(_db) {
            OnLitCreated = () => {
                RefreshChambres();
                RefreshDashboard();
                SetStatus("✅ Nouveau lit installé avec succès", Color.FromRgb(0, 212, 170));
                LogEvent("🛏 Ajout d'un nouveau lit dans l'inventaire");
            },
            OnCancelled = () => { }
        };
        addLitForm.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        addLitForm.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnAddChambre_Click(object sender, RoutedEventArgs e) {
        var addChambreForm = new AddChambreForm(_db) {
            OnChambreCreated = () => {
                SetStatus("✅ Nouvelle chambre construite avec succès", Color.FromRgb(0, 212, 170));
                LogEvent("🏗 Construction d'une nouvelle chambre terminée");
                RefreshAllData();
            },
            OnCancelled = () => { }
        };
        addChambreForm.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        addChambreForm.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnRemoveLit_Click(object sender, RoutedEventArgs e) {
        if (sender is not FrameworkElement fe || fe.DataContext is not LitInventaire lit) return;

        var deletePopup = PopupFactory.CreateConfirmationPopup(
            $"Êtes-vous sûr de vouloir supprimer le lit {lit.NumeroLit} ?",
            "#FF4D6A",
            () => {
                _db.RemoveLit(lit.IdLit);
                LogEvent($"🗑 Lit {lit.NumeroLit} détruit");
                RefreshChambres();
                RefreshDashboard();
            },
            () => { }
        );
        deletePopup.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        deletePopup.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }

    private void BtnRemoveChambre_Click(object sender, RoutedEventArgs e) {
        if (sender is not FrameworkElement fe || fe.DataContext is not ChambreGroup chambre) return;

        var deletePopup = PopupFactory.CreateConfirmationPopup(
            $"Êtes-vous sûr de vouloir supprimer la chambre {chambre.NumeroChambre} ?",
            "#FF4D6A",
            () => {
                _db.RemoveChambre(chambre.IdChambre);
                LogEvent($"🗑 Chambre {chambre.NumeroChambre} supprimée");
                RefreshChambres();
                RefreshDashboard();
                SetStatus($"✅ Chambre {chambre.NumeroChambre} supprimée", Color.FromRgb(255, 77, 106));
            },
            () => { }
        );

        deletePopup.Owner = this;
        DimOverlay.Visibility = Visibility.Visible;
        deletePopup.ShowDialog();
        DimOverlay.Visibility = Visibility.Collapsed;
    }
    private Window? _dragWindow;

    private void Lit_MouseMove(object sender, MouseEventArgs e) {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe &&
            fe.DataContext is LitInventaire lit) {
            _dragWindow = new Window {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Topmost = true,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Opacity = 0.8,
                Content = new Border {
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock {
                        Text = "🛏️ " + lit.NumeroLit,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold
                    }
                }
            };

            var mousePos = PointToScreen(e.GetPosition(this));
            double dpiX = 1.0;
            double dpiY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null) {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }
            _dragWindow.Left = (mousePos.X / dpiX) + 15;
            _dragWindow.Top = (mousePos.Y / dpiY) + 15;
            _dragWindow.Show();

            fe.GiveFeedback += Fe_GiveFeedback;
            DragDrop.DoDragDrop(fe, lit, DragDropEffects.Move);
            fe.GiveFeedback -= Fe_GiveFeedback;

            _dragWindow.Close();
            _dragWindow = null;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(ref Win32Point pt);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Win32Point {
        public int X;
        public int Y;
    }

    private void Fe_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
        if (_dragWindow != null) {
            Win32Point pt = new Win32Point();
            GetCursorPos(ref pt);
            double dpiX = 1.0;
            double dpiY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null) {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }
            _dragWindow.Left = (pt.X / dpiX) + 15;
            _dragWindow.Top = (pt.Y / dpiY) + 15;
        }
    }

    private void SvChambres_DragOver(object sender, DragEventArgs e) {
        DragDropHelper.HandleAutoScroll(SvChambres, e);
    }

    private void Chambre_Drop(object sender, DragEventArgs e) {
        if (sender is FrameworkElement fe && fe.DataContext is ChambreGroup chambreCible)
            if (e.Data.GetData(typeof(LitInventaire)) is LitInventaire litDragged) {
                if (litDragged.IdChambre == chambreCible.IdChambre) return;

                _db.UpdateLitChambre(litDragged.IdLit, chambreCible.IdChambre);
                LogEvent($"🛏 Lit {litDragged.NumeroLit} transféré vers {chambreCible.NumeroChambre}");
                RefreshChambres();
            }
    }

    private void Lit_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
        if (sender is FrameworkElement fe && fe.DataContext is LitInventaire lit && fe.ContextMenu != null) {
            fe.ContextMenu.Items.Clear();

            var menuTitle = new MenuItem { Header = "Assigner à la chambre...", IsEnabled = false };
            fe.ContextMenu.Items.Add(menuTitle);
            fe.ContextMenu.Items.Add(new Separator());

            foreach (var chambre in _chambresGroupes) {
                if (chambre.IdChambre == lit.IdChambre) continue;

                var menuItem = new MenuItem {
                    Header = chambre.NumeroChambre,
                    Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 245))
                };

                menuItem.Click += (s, args) => {
                    _db.UpdateLitChambre(lit.IdLit, chambre.IdChambre);
                    LogEvent($"🛏 Lit {lit.NumeroLit} réassigné à {chambre.NumeroChambre}");
                    RefreshChambres();
                };

                fe.ContextMenu.Items.Add(menuItem);
            }

            fe.ContextMenu.Items.Add(new Separator());

            var deleteItem = new MenuItem {
                Header = "Supprimer le lit",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 106))
            };

            deleteItem.Click += (s, args) => {
                _db.RemoveLit(lit.IdLit);
                LogEvent($"🗑 Lit {lit.NumeroLit} détruit");
                RefreshChambres();
            };

            fe.ContextMenu.Items.Add(deleteItem);
        }
    }

    private void RefreshMedicaments() {
        var list = _db.GetMedicaments();
        LvMedicaments.ItemsSource = list;
        var lowStock = list.Where(m => m.StockActuel < m.StockMinimum).ToList();
        foreach (var m in lowStock) {
            LogEvent($"⚠️ Stock bas: {m.Nom} ({m.StockActuel}/{m.StockMinimum})");
        }
    }

    private System.Collections.Generic.List<FactureDisplayItem> _allInvoices = new();
    private string _activeInvoiceFilter = "Tous";

    private void RefreshFactures() {
        var patientsInvoices = _db.GetFacturesPatient();
        var hospitalInvoices = _db.GetFacturesHopital();
        _allInvoices = patientsInvoices.Concat(hospitalInvoices).OrderByDescending(f => f.JourEmission).ToList();
        ApplyInvoiceFilter();
    }

    private void ApplyInvoiceFilter() {
        if (_activeInvoiceFilter == "Patient") {
            LvFactures.ItemsSource = _allInvoices.Where(f => f.Type == "Patient").ToList();
        } else if (_activeInvoiceFilter == "Hopital") {
            LvFactures.ItemsSource = _allInvoices.Where(f => f.Type == "Hopital").ToList();
        } else {
            LvFactures.ItemsSource = _allInvoices;
        }
    }

    private void BtnFilterInvoices_Click(object sender, RoutedEventArgs e) {
        if (sender is Button btn) {
            _activeInvoiceFilter = btn.Tag.ToString() ?? "Tous";
            
            BtnFilterAllInvoices.Background = new SolidColorBrush(Color.FromRgb(42, 42, 56));
            BtnFilterPatientInvoices.Background = new SolidColorBrush(Color.FromRgb(42, 42, 56));
            BtnFilterHospitalInvoices.Background = new SolidColorBrush(Color.FromRgb(42, 42, 56));
            
            btn.Background = new SolidColorBrush(Color.FromRgb(58, 58, 77));
            ApplyInvoiceFilter();
        }
    }

    private void BtnOuvrirFactureHtml_Click(object sender, RoutedEventArgs e) {
        if (sender is Button btn && btn.Tag is FactureDisplayItem item) {
            string path = "";
            if (item.Type == "Patient") {
                var p = _db.GetPatients().FirstOrDefault(pat => pat.IdPatient == item.IdRealFacture);
                if (p == null) {
                    p = new PatientActif { Nom = item.PatientNom, Classe = "Inconnue" };
                }
                var hosp = _db.GetHospital();
                var lignes = _db.GetLignesFacture(item.IdRealFacture);
                var dummyFacture = new Facture {
                    IdFacture = item.IdRealFacture,
                    CodeFacture = item.CodeFacture,
                    JourEmission = item.JourEmission,
                    MontantTotal = item.MontantTotal,
                    Statut = item.Statut,
                    PatientNom = item.PatientNom
                };
                path = dummyFacture.CheminFichier ?? "";
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) {
                    if (hosp != null) {
                        path = Services.FactureGenerator.ExporterFactureHtml(dummyFacture, p, hosp, lignes);
                    }
                }
            } else if (item.Type == "Hopital") {
                var hosp = _db.GetHospital();
                if (hosp != null) {
                    var lignes = _db.GetLignesFactureHopital(item.IdRealFacture);
                    path = Services.FactureGenerator.ExporterFactureHopitalHtml(item, hosp, lignes.Count > 0 ? lignes : null);
                }
            }

            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            } else {
                PopupFactory.ShowAlert(this, "Le fichier HTML de la facture est introuvable.");
            }
        }
    }

    private void RefreshHitParade() {
        LvHitParade.ItemsSource = _db.GetHitParade();
    }

    private void RefreshReservations() {
        LvReservations.ItemsSource = _db.GetReservations();
    }

    private void BtnAddMedicament_Click(object sender, RoutedEventArgs e) {
        var form = new AddMedicamentForm(_db) { Owner = this };
        if (form.ShowDialog() == true) {
            RefreshMedicaments();
            LogEvent("💊 Nouveau médicament ajouté au catalogue");
        }
    }

    private void LvMedicaments_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
        if (LvMedicaments.SelectedItem is not Medicament medicament) return;

        var fe = e.OriginalSource as FrameworkElement;
        if (fe == null) return;

        fe.ContextMenu = new ContextMenu();
        fe.ContextMenu.Background = new SolidColorBrush(Color.FromRgb(28, 28, 34));
        fe.ContextMenu.Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 245));
        fe.ContextMenu.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 56));

        var editItem = new MenuItem { Header = "Modifier" };
        editItem.Click += (s, args) => {
            var form = new AddMedicamentForm(_db, medicament) { Owner = this };
            if (form.ShowDialog() == true) {
                RefreshMedicaments();
                LogEvent($"💊 Médicament {medicament.Nom} modifié");
            }
        };

        var delItem = new MenuItem { Header = "Supprimer", Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 106)) };
        delItem.Click += (s, args) => {
            _db.RemoveMedicament(medicament.IdMedicament);
            RefreshMedicaments();
            LogEvent($"🗑 Médicament {medicament.Nom} supprimé");
        };

        fe.ContextMenu.Items.Add(editItem);
        fe.ContextMenu.Items.Add(delItem);
    }

    private void RefreshSalleAttente() {
        var patients = _db.GetWaitingRoomPatients();
        IcSalleAttente.ItemsSource = patients;
        _db.GetWaitingRoomStats(out int gen, out int adm, out double ratio);
        TbRatioAdmission.Text = gen > 0 ? $"{ratio:F1} %" : "0 %";
    }

    private void BtnAdmettrePatientAttente_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button btn || btn.Tag == null) return;
        
        int idPatientAttente = 0;
        if (btn.Tag is int tagInt) {
            idPatientAttente = tagInt;
        } else if (!int.TryParse(btn.Tag.ToString(), out idPatientAttente)) {
            return;
        }

        var patient = _db.GetWaitingRoomPatient(idPatientAttente);
        if (patient == null) return;

        var dlg = new AdmitWaitingRoomPatientWindow(_db, patient);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true) {
            RefreshSalleAttente();
            RefreshPatients();
            RefreshDashboard();
            SetStatus($"✅ Patient {patient.NomComplet} admis avec succès !", Color.FromRgb(0, 212, 170));
        }
    }

    private void LoadTableData(string tableName) {
        _selectedTableName = tableName;
        try {
            using (var conn = new MySqlConnector.MySqlConnection(_db.ConnectionString)) {
                conn.Open();
                string query = $"SELECT * FROM `{tableName}`";
                _currentAdapter = new MySqlConnector.MySqlDataAdapter(query, conn);
                _currentCommandBuilder = new MySqlConnector.MySqlCommandBuilder(_currentAdapter);
                
                _currentTable = new System.Data.DataTable();
                _currentAdapter.Fill(_currentTable);
                
                DgAdminCRUD.ItemsSource = _currentTable.DefaultView;
                SetStatus($"Table `{tableName}` chargée ({_currentTable.Rows.Count} lignes).", Color.FromRgb(0, 212, 170));
            }
        } catch (Exception ex) {
            MessageBox.Show($"Erreur lors du chargement de la table `{tableName}` : {ex.Message}", "Erreur SQL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CbAdminTables_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (CbAdminTables == null || CbAdminTables.SelectedItem is not ComboBoxItem item) return;
        LoadTableData(item.Content.ToString() ?? "");
    }

    private void BtnAdminRefresh_Click(object sender, RoutedEventArgs e) {
        if (!string.IsNullOrEmpty(_selectedTableName)) {
            LoadTableData(_selectedTableName);
        }
    }

    private void BtnAdminSave_Click(object sender, RoutedEventArgs e) {
        if (_currentAdapter == null || _currentTable == null) return;
        try {
            using (var conn = new MySqlConnector.MySqlConnection(_db.ConnectionString)) {
                conn.Open();
                if (_currentAdapter.SelectCommand != null) {
                    _currentAdapter.SelectCommand.Connection = conn;
                }
                _currentAdapter.Update(_currentTable);
                SetStatus("Modifications enregistrées avec succès.", Color.FromRgb(0, 212, 170));
                MessageBox.Show("Modifications enregistrées dans la base de données !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAllData();
            }
        } catch (Exception ex) {
            MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}", "Erreur SQL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateCartTotals() {
        if (_cart.Count == 0) {
            TbCartSubtotal.Text = "$ 0.00";
            TbCartTax.Text = "$ 0.00";
            TbCartTotal.Text = "$ 0.00";
            return;
        }

        decimal subtotal = _cart.Sum(item => item.PrixAchatTotal);
        int uniqueIds = _cart.Select(item => item.Medicament.IdMedicament).Distinct().Count();
        decimal tax = 1000.0m / uniqueIds;
        decimal total = subtotal + tax;

        TbCartSubtotal.Text = $"$ {subtotal:N2}";
        TbCartTax.Text = $"$ {tax:N2}";
        TbCartTotal.Text = $"$ {total:N2}";
    }

    private void BtnCommanderMedicament_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button btn || btn.Tag is not Medicament med) return;

        var orderWindow = new OrderMedicamentWindow(med);
        orderWindow.Owner = this;
        if (orderWindow.ShowDialog() == true) {
            int qty = orderWindow.Quantity;
            
            var existing = _cart.FirstOrDefault(item => item.Medicament.IdMedicament == med.IdMedicament);
            if (existing != null) {
                existing.Quantite += qty;
                int idx = _cart.IndexOf(existing);
                _cart[idx] = new CartItem { Medicament = med, Quantite = existing.Quantite };
            } else {
                _cart.Add(new CartItem { Medicament = med, Quantite = qty });
            }

            UpdateCartTotals();
            SetStatus($"🛒 {qty}x {med.Nom} ajoutés au panier.", Color.FromRgb(0, 212, 170));
        }
    }

    private void BtnRemoveFromCart_Click(object sender, RoutedEventArgs e) {
        if (sender is not Button btn || btn.Tag is not CartItem item) return;
        _cart.Remove(item);
        UpdateCartTotals();
        SetStatus($"🗑️ {item.Nom} retiré du panier.", Colors.OrangeRed);
    }

    private void BtnCheckout_Click(object sender, RoutedEventArgs e) {
        if (_cart.Count == 0) {
            PopupFactory.ShowAlert(this, "Le panier est vide !");
            return;
        }

        decimal subtotal = _cart.Sum(item => item.PrixAchatTotal);
        int uniqueIds = _cart.Select(item => item.Medicament.IdMedicament).Distinct().Count();
        decimal tax = 1000.0m / uniqueIds;
        decimal totalCost = subtotal + tax;

        _hospital = _db.GetHospital();
        if (_hospital == null) return;

        if (_hospital.Budget < totalCost) {
            PopupFactory.ShowAlert(this, $"Fonds insuffisants ! Budget: $ {_hospital.Budget:N2} | Requis: $ {totalCost:N2}", "#FF4D6A");
            return;
        }

        try {
            int currentDay = (_simulationService.GameTime - new DateTime(2026, 1, 1)).Days + 1;
            _db.AddBudget(-totalCost, "Commande de médicaments (Panier global)", currentDay, "Achat Médicaments");

            var random = new Random();
            foreach (var item in _cart) {
                int baseDelay = item.Medicament.TempsLivraisonBase;
                double factor = random.NextDouble() * 1.0 + 0.5; 
                int actualDelay = (int)Math.Max(1.0, baseDelay * factor);

                _db.AddDelivery(item.Medicament.IdMedicament, item.Quantite, actualDelay, item.PrixAchatTotal);
                LogEvent($"📦 Commande passée: {item.Quantite}x {item.Nom} (Délai estimé: {actualDelay} min).");
            }

            _db.CreateFactureHopital("Achat Médicaments", totalCost, $"Commande globale de médicaments : {string.Join(", ", _cart.Select(c => $"{c.Quantite}x {c.Nom}"))}. Inclut la taxe d'importation dégressive.");

            _cart.Clear();
            UpdateCartTotals();
            RefreshAll();
            RefreshMedicaments();

            MessageBox.Show("Commande passée avec succès ! Les livraisons arriveront prochainement.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus("✅ Commande globale validée !", Color.FromRgb(0, 212, 170));
        } catch (Exception ex) {
            PopupFactory.ShowAlert(this, "Erreur lors de la validation de la commande: " + ex.Message);
        }
    }

    public void ShowToast(string message, string colorName) {
        Dispatcher.Invoke(() => {
            var toast = new ToastNotification { Message = message, ColorName = colorName };
            _notifications.Add(toast);
            
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => {
                _notifications.Remove(toast);
                timer.Stop();
            };
            timer.Start();
        });
    }

    private void RefreshProces() {
        try {
            var proces = _db.GetProces();
            DgProces.ItemsSource = proces;
            TbProcesEnCours.Text = proces.Count(p => p.Statut == "En cours").ToString();
            TbProcesLost.Text = proces.Count(p => p.Statut == "Perdu").ToString();
            decimal totalFines = proces.Where(p => p.Statut == "Perdu").Sum(p => p.MontantPenalite);
            TbProcesPenalties.Text = $"$ {totalFines:N0}";
        } catch (Exception ex) {
            SetStatus($"Erreur Procès : {ex.Message}", Color.FromRgb(255, 77, 106));
        }
    }

    private void Nav_Click_Hit(object sender, RoutedEventArgs e) {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://youtu.be/bIOEkv7PibM?t=82") { UseShellExecute = true });
    }
}

public class ToastNotification {
    public string Message { get; set; } = "";
    public string ColorName { get; set; } = "Ambre"; 
    public Brush BackgroundBrush {
        get {
            return ColorName switch {
                "Ambre" => new SolidColorBrush(Color.FromRgb(255, 179, 71)), 
                "Vert" => new SolidColorBrush(Color.FromRgb(0, 212, 170)),  
                "Rouge" => new SolidColorBrush(Color.FromRgb(255, 77, 106)), 
                "Bleu" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  
                _ => new SolidColorBrush(Color.FromRgb(42, 42, 56))          
            };
        }
    }
}