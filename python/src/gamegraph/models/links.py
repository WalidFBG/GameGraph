import math

import torch

from gamegraph.models.contract import Link


def inverse(link: Link, eta: torch.Tensor) -> torch.Tensor:
    match link:
        case Link.IDENTITY:
            return eta
        case Link.LOG:
            return torch.exp(eta)
        case Link.LOGIT:
            return torch.sigmoid(eta)


def apply(link: Link, value: float) -> float:
    match link:
        case Link.IDENTITY:
            return value
        case Link.LOG:
            return math.log(value)
        case Link.LOGIT:
            return math.log(value / (1 - value))
