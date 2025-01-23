import os
from supabase import create_client, Client
import requests
import time
from datetime import datetime

# import schedule

# create supabase client
url: str = os.environ.get('SupabaseUrl__FNF')
key: str = os.environ.get('SupabaseKeySR__FNF')
supabase: Client = create_client(url, key)

# pull players from db
response = supabase.table('players').select('nhl_id').execute()
players = response.data

# get player ids
player_ids = [player['nhl_id'] for player in players]
print(f'{len(player_ids)} queried from Supabase')


# this gets blocks and hits
def fetch_realtime_stats_from_nhl(ids: list):
    # example url
    # https://api.nhle.com/stats/rest/en/skater/realtime?limit=-1&cayenneExp=seasonId=20242025 and gameTypeId=2 and (playerId=8480036 or playerId=8480039)

    base_url = 'https://api.nhle.com/stats/rest/en/skater/realtime?limit=-1&cayenneExp=seasonId=20242025 and gameTypeId=2 '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for realtime stats')
    return response.json()['data'] if response.status_code == 200 else []


# blocks and hits
def update_table_player_realtime_stats(stats):
    for stat in stats:
        entry = {
            'hits': stat['hits'],
            'blocks': stat['blockedShots'],
            'stats_last_updated': str(datetime.now())
        }

        response = supabase.table('players').update(entry).eq('nhl_id', stat['playerId']).execute()


def fetch_player_stats_from_nhl(ids: list):
    # example url
    # https://api.nhle.com/stats/rest/en/skater/summary?cayenneExp=seasonId=20242025 and playerId=8480039

    base_url = 'https://api.nhle.com/stats/rest/en/skater/summary?limit=-1&cayenneExp=seasonId=20242025 '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for player stats')
    return response.json()['data'] if response.status_code == 200 else []


def update_table_player_stats(stats):
    for stat in stats:
        entry = {
            'goals': stat['goals'],
            'assists': stat['assists'],
            'pp_points': stat['ppPoints'],
            'sh_points': stat['shPoints'],
            'shots_on_goal': stat['shots'],
            'games_played': stat['gamesPlayed'],
            'stats_last_updated': str(datetime.now())
        }

        response = supabase.table('players').update(entry).eq('nhl_id', stat['playerId']).execute()


def fetch_goalie_stats_from_nhl(ids: list):
    # example url
    # https://api.nhle.com/stats/rest/en/goalie/summary?cayenneExp=seasonId=20242025 and playerId=8480045

    base_url = 'https://api.nhle.com/stats/rest/en/goalie/summary?cayenneExp=seasonId=20242025 '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for goalie stats')
    return response.json()['data'] if response.status_code == 200 else []


def update_table_goalie_stats(stats):
    for stat in stats:
        entry = {
            'goalie_wins': stat['wins'],
            'goalie_gaa': stat['goalsAgainstAverage'],
            'goalie_sv_pctg': stat['savePct'],
            'games_played': stat['gamesPlayed'],
            'goalie_shutouts': stat['shutouts'],
            'goalie_saves': stat['saves'],
            'goalie_goals_against': stat['goalsAgainst'],
            'stats_last_updated': str(datetime.now())
        }

        response = supabase.table('players').update(entry).eq('nhl_id', stat['playerId']).execute()


if __name__ == '__main__':

    while True:
        realtime_stats = fetch_realtime_stats_from_nhl(player_ids)
        print(f'{len(realtime_stats)} realtime stats queried from NHL')
        update_table_player_realtime_stats(realtime_stats)
        print('------------------\n')

        player_stats = fetch_player_stats_from_nhl(player_ids)
        print(f'{len(player_stats)} players queried from NHL')
        update_table_player_stats(player_stats)
        print('------------------\n')

        goalie_stats = fetch_goalie_stats_from_nhl(player_ids)
        print(f'{len(goalie_stats)} goalies queried from NHL')
        update_table_goalie_stats(goalie_stats)
        print('------------------\n')

        # sleep for 10 min
        print('\nsleeping...\n')
        time.sleep(60 * 10)