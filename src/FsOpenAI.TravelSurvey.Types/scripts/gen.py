import pandas as pd

# Load the trips data
trips_df = pd.read_csv('e:/s/nhts/csv/tripv2pub.csv')

# Filter trips that are for work purposes (Home-based work)
work_trips_df = trips_df[trips_df['TRIPPURP'] == 1]

# Check if these trips have more than one person in the vehicle
carpool_trips_df = work_trips_df[work_trips_df['NUMONTRP'] > 1]

# Calculate the percentage of carpooling trips for work
total_work_trips = len(work_trips_df)
carpool_work_trips = len(carpool_trips_df)
percentage_carpool = (carpool_work_trips / total_work_trips) * 100

print(f"Percentage of times people carpool together for work: {percentage_carpool:.2f}%")