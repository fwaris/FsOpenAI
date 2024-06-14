namespace FsOpenAI.TravelSurvey.Types
(*
#r "nuget: FSharp.Data.CSV.Core"
*)
open System

// Trip Purpose
type TripPurpose =
    | NotAscertained // Not ascertained
    | DontKnow // Don’t know
    | Refused // Refused
    | RegularActivitiesAtHome // Regular activities at home
    | WorkFromHome // Work from home
    | WorkAtNonHomeLocation // Work at a non-home location
    | WorkActivityDropOffPickup // Work activity to drop-off/pickup someone/something
    | OtherWorkRelatedActivities // Other work-related activities
    | AttendSchoolAsStudent // Attend school as a student
    | AttendChildOrAdultCare // Attend child or adult care
    | VolunteerActivities // Volunteer activities
    | ChangeTransportationType // Change transportation type
    | DropOffPickupSomeone // Drop off/pick up someone
    | HealthCareVisit // Health care visit
    | BuyMeals // Buy meals
    | ShopBuyPickupReturnGoods // Shop/buy/pick-up or return goods
    | OtherFamilyPersonalErrands // Other family/personal errands
    | RecreationalActivities // Recreational activities
    | Exercise // Exercise
    | VisitFriendsOrRelatives // Visit friends or relatives
    | ReligiousOrCommunityActivities // Religious or other community activities
    | RestOrRelaxationVacation // Rest or relaxation/vacation
    | SomethingElse // Something else

type MeansOfTransportation =
    | Car // Car
    | Van // Van
    | SUVCrossover // SUV/Crossover
    | PickUpTruck // Pick Up Truck
    | Minivan // Van (Minivan)
    | RecreationalVehicle // Recreational Vehicle
    | Motorcycle // Motorcycle
    | MotorcycleMoped //Motorcycle/Moped
    | RV // RV (Motorhome, ATV, Snowmobile)
    | StreetcarOrTrolleyCar // Streetcar or trolley car
    | SubwayOrElevatedRail // Subway or Elevated Rail
    | CommuterRail // Commuter Rail
    | PrivateCharterTourShuttleBus // Private/Charter/Tour/Shuttle Bus
    | Airplane // Airplane
    | TaxicabOrLimoService // Taxicab or limo service
    | OtherRideSharingServices // Other ride-sharing services
    | ParatransitDial

type Household = {
    /// Unique identifier for the household
    HOUSEID: string

    /// Number of people in the household
    HHSIZE: int

    /// Number of vehicles in the household
    VEHCOUNT: int

    /// Household income level
    INCOME: string

    /// Type of residence (e.g., single-family home, apartment)
    RESIDENCE_TYPE: string

    /// Urban or rural status of the household
    URBAN_RURAL: string

    /// State where the household is located
    STATE: string

    /// Census division where the household is located
    CENSUS_DIVISION: string

    /// Whether the household has internet access
    INTERNET_ACCESS: bool

    /// Number of workers in the household
    WORKER_COUNT: int

    /// Number of children in the household
    CHILD_COUNT: int

    /// Number of elderly members in the household
    ELDERLY_COUNT: int

    /// Whether the household owns or rents their residence
    OWN_RENT: string

    /// Whether the household has any members with disabilities
    DISABILITY: bool

    /// Whether the household has any members who are students
    STUDENT: bool

    /// Whether the household has any members who are retired
    RETIRED: bool

    /// Whether the household has any members who are unemployed
    UNEMPLOYED: bool

    /// Whether the household has any members who work from home
    WORK_FROM_HOME: bool

    /// Whether the household has any members who use public transportation
    PUBLIC_TRANSPORTATION: bool

    /// Whether the household has any members who bike or walk to work
    BIKE_WALK: bool

    /// Whether the household has any members who use ride-sharing services
    RIDE_SHARING: bool

    /// Whether the household has any members who use e-scooters
    E_SCOOTER: bool
}

/// Represents a vehicle associated with a household
type Vehicle = {
    /// Unique identifier for the household
    HOUSEID: string
    /// Unique identifier for the vehicle within the household
    VEHID: string
    /// Make of the vehicle (e.g., Toyota, Ford)
    Make: string
    /// Model of the vehicle (e.g., Camry, F-150)
    Model: string
    /// Year the vehicle was manufactured
    Year: int
    /// Type of fuel the vehicle uses (e.g., Gasoline, Diesel, Electric)
    FuelType: string
    /// Indicates if the vehicle is used for commercial purposes
    IsCommercial: bool
    /// Indicates if the vehicle is a rental
    IsRental: bool
    /// Additional comments or notes about the vehicle
    Comments: string option
}

type Person = {
    /// Unique identifier for the household
    HOUSEID: string
    /// Unique identifier for the person within the household
    PERSONID: string
    /// Age of the person
    Age: int
    /// Gender of the person
    Gender: string
    /// Employment status of the person
    EmploymentStatus: string
    /// Whether the person has a driver's license
    HasDriversLicense: bool
    /// Number of trips made by the person on the travel day
    NumberOfTrips: int
    /// Total distance traveled by the person on the travel day (in miles)
    TotalDistanceTraveled: float
    /// Mode of transportation used by the person for the majority of their trips
    PrimaryModeOfTransport: string
    /// Whether the person worked from home on the travel day
    WorkedFromHome: bool
    /// Any other relevant demographic attributes
    OtherDemographicAttributes: string
}

type Trip = {
    /// Unique identifier for the household
    HOUSEID: string
    /// Unique identifier for the person within the household
    PERSONID: string
    /// Unique identifier for the trip
    TRIPID: string
    /// Start time of the trip
    STARTTIME: string
    /// End time of the trip
    ENDTIME: string
    /// Origin of the trip
    ORIGIN: string
    /// Destination of the trip
    DESTINATION: string
    /// Mode of transportation used for the trip
    TRPTRANS: string
    /// Purpose of the trip
    WHYTO: string
    /// Purpose of the trip from the previous location
    WHYFROM: string
    /// Distance of the trip in miles
    TRPMILES: float
    /// Duration of the trip in minutes
    TRPDUR: float
    /// Flag indicating if the trip is a long-distance trip
    LONGDIST: bool
    /// Number of people in the vehicle during the trip
    VEHOC: int
    /// Flag indicating if the respondent was the driver
    DRVR_FLG: bool
    /// Additional comments or notes about the trip
    COMMENTS: string option
}

module Helpers =

    //format a number to string
    let formatNumber (f:float) : string = if Double.IsNaN(f) then "n/a" else f.ToString("N2")

    //format a number to string with % sign at end
    let formatNumberPercent (f:float) : string = if Double.IsNaN(f) then "n/a" else sprintf($"%0.2f{f}")

    
module Data =
    open FSharp.Data
    let private rootPath = @"E:\s\nhts\csv"
    let private (@@) (a:string) (b:string) = System.IO.Path.Combine(a,b)
    let households() : Household list =
        let path = rootPath @@ "hhv2pub.csv"
        let fn = CsvFile.Load(path)
        fn.Rows
        |> Seq.map(fun r -> 
            {
                HOUSEID = r.[0]
                HHSIZE = int r.[1]
                VEHCOUNT = int r.[2]
                INCOME = r.[3]
                RESIDENCE_TYPE = r.[4]
                URBAN_RURAL = r.[5]
                STATE = r.[6]
                CENSUS_DIVISION = r.[7]
                INTERNET_ACCESS = Boolean.Parse r.[8]
                WORKER_COUNT = int r.[9]
                CHILD_COUNT = int r.[10]
                ELDERLY_COUNT = int r.[11]
                OWN_RENT = r.[12]
                DISABILITY = Boolean.Parse r.[13]
                STUDENT = Boolean.Parse r.[14]
                RETIRED = Boolean.Parse r.[15]
                UNEMPLOYED = Boolean.Parse r.[16]
                WORK_FROM_HOME = Boolean.Parse r.[17]
                PUBLIC_TRANSPORTATION = Boolean.Parse r.[18]
                BIKE_WALK = Boolean.Parse r.[19]
                RIDE_SHARING = Boolean.Parse r.[20]
                E_SCOOTER = Boolean.Parse r.[21]
        })
        |> Seq.toList

    let vehicles() : Vehicle list =
        let path = rootPath @@ "vehv2pub.csv"
        let fn = CsvFile.Load(path)
        fn.Rows
        |> Seq.map(fun r -> 
            {
                HOUSEID = r.[0]
                VEHID = r.[1]
                Make = r.[2]
                Model = r.[3]
                Year = int r.[4]
                FuelType = r.[5]
                IsCommercial = Boolean.Parse r.[6]
                IsRental = Boolean.Parse r.[7]
                Comments = let s = r.[8] in if String.IsNullOrWhiteSpace(s) then None else s.Trim() |> Some
            })
        |> Seq.toList

    let persons() : Person list =
        let path = rootPath @@ "perv2pub.csv"
        let fn = CsvFile.Load(path)
        fn.Rows
        |> Seq.map(fun r -> 
            {
                HOUSEID = r.[0]
                PERSONID = r.[1]
                Age = int r.[2]
                Gender = r.[3]
                EmploymentStatus = r.[4]
                HasDriversLicense = Boolean.Parse r.[5]
                NumberOfTrips = int r.[6]
                TotalDistanceTraveled = float r.[7]
                PrimaryModeOfTransport = r.[8]
                WorkedFromHome = Boolean.Parse r.[9]
                OtherDemographicAttributes = r.[10]
            })
        |> Seq.toList

    let trips() : Trip list =
        let path = rootPath @@ "tripv2pub.csv"
        let fn = CsvFile.Load(path)
        fn.Rows
        |> Seq.map(fun r -> 
            {
                HOUSEID = r.[0]
                PERSONID = r.[1]
                TRIPID = r.[2]
                STARTTIME = r.[3]
                ENDTIME = r.[4]
                ORIGIN = r.[5] 
                DESTINATION = r.[6]
                TRPTRANS = r.[7]
                WHYTO = r.[8]
                WHYFROM = r.[9]
                TRPMILES = float r.[10]
                TRPDUR = float r.[11]
                LONGDIST = Boolean.Parse r.[12]
                VEHOC = int r.[13]
                DRVR_FLG = Boolean.Parse r.[14]
                COMMENTS = let s = r.[15] in if String.IsNullOrWhiteSpace s then None else s.Trim() |> Some
            })
        |> Seq.toList

(*
Data.households()
*)