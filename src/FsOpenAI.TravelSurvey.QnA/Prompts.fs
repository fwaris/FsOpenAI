namespace FsOpenAI.TravelSurvey

module Prompts = 
    let fsiTypes = """
namespace FsOpenAI.TravelSurvey.Types

// Define a record type for Household data
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

"""